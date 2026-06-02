using System.Text;
using System.Text.Json;
using PolyInstall.Cli.Validation;
using PolyInstall.Core.Build;
using PolyInstall.Core.Globbing;
using PolyInstall.Core.Install;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Payload;

namespace PolyInstall.Cli.Build;

/// <summary>
/// Builds self-extracting installers by appending manifest + compressed payload to a pre-published
/// PolyInstall.Runtime stub binary. On Windows, also requires <c>PolyInstall.Uninstall.exe</c> next to the stub;
/// the build pipeline adds it to the zip payload under <c>.polyinstall/tools/</c> so the installer can copy it to
/// <c>Uninstall.exe</c> and register Add/Remove Programs.
/// </summary>
public static class InstallerBuildPipeline
{
    public static async Task RunAsync(
        string manifestPath,
        string baseDirectory,
        string? stubsRoot,
        CancellationToken ct)
    {
        baseDirectory = Path.GetFullPath(baseDirectory);
        manifestPath = Path.GetFullPath(manifestPath);
        BuildLog.Info($"Building from manifest {manifestPath}");
        BuildLog.Info($"Base directory: {baseDirectory}");
        if (!string.IsNullOrWhiteSpace(stubsRoot))
            BuildLog.VerboseLine($"Stubs directory (explicit): {Path.GetFullPath(stubsRoot)}");

        BuildLog.Info("Reading and parsing manifest…");
        var yaml = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = ManifestYaml.Parse(yaml);
        BuildLog.Info("Applying environment variable substitution…");
        manifest = EnvironmentSubstitution.ApplyToManifest(manifest);
        BuildLog.VerboseLine($"Product: {manifest.Metadata.Name} v{manifest.Metadata.Version}");

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema", "v1.json");
        if (!File.Exists(schemaPath))
            schemaPath = Path.Combine(FindRepoSchema(), "v1.json");
        BuildLog.Info($"Validating manifest against schema ({schemaPath})…");
        var json = JsonSerializer.Serialize(manifest, InstallManifest.JsonOptions);
        ManifestJsonValidator.Validate(json, schemaPath);
        BuildLog.Info("Manifest validation passed.");

        BuildLog.Info("Collecting files from manifest entries…");
        var baseFiles = new List<(string EntryName, string FullPath)>();
        foreach (var entry in manifest.Files)
        {
            var globs = GlobResolver.Collect(baseDirectory, entry.SourceDir, entry.Include, entry.Exclude);
            var excludeList = entry.Exclude is { } ex ? string.Join(", ", ex) : "(none)";
            BuildLog.VerboseLine(
                $"  {entry.SourceDir}: {globs.Count} file(s) (include: {string.Join(", ", entry.Include)}; exclude: {excludeList})");
            foreach (var g in globs)
                baseFiles.Add((g.RelativePath, g.FullPath));
        }

        if (baseFiles.Count == 0)
            throw new InvalidOperationException("No files matched manifest files entries; nothing to pack.");

        BuildLog.Info($"Collected {baseFiles.Count} file(s) for the payload.");

        var compression = PayloadArchive.ParseCompression(manifest.Build.Compression);
        BuildLog.Info($"Payload compression: {compression}");

        var outDir = Path.GetFullPath(Path.Combine(baseDirectory, manifest.Build.OutputDir));
        Directory.CreateDirectory(outDir);
        BuildLog.Info($"Output directory: {outDir}");

        var stubRoot = ResolveStubRoot(baseDirectory, stubsRoot);
        BuildLog.Info($"Stubs root: {stubRoot}");
        BuildLog.Info($"Build targets: {string.Join(", ", manifest.Build.Targets)}");

        foreach (var target in manifest.Build.Targets)
        {
            ct.ThrowIfCancellationRequested();
            BuildLog.Info($"--- Target: {target} ---");
            manifest.Build.InstallerTarget = target;
            var rid = RidMapping.ToDotNetRid(target);
            BuildLog.VerboseLine($"  .NET RID: {rid}");
            var stubPath = ResolveStubPath(manifest, stubRoot, rid);
            if (!File.Exists(stubPath))
                throw new FileNotFoundException($"Stub binary not found for target '{target}' (RID {rid}): {stubPath}. Publish PolyInstall.Runtime for this RID into stubs/{rid}/.");

            StubPublishValidator.ValidateRuntimeStub(stubPath);
            var stubSize = new FileInfo(stubPath).Length;
            BuildLog.Info($"Using runtime stub: {stubPath} ({BuildLog.FormatBytes(stubSize)})");

            var isWinTarget = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase);
            var ext = isWinTarget ? ".exe" : "";
            var safeName = string.Join("_", manifest.Metadata.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (string.IsNullOrEmpty(safeName))
                safeName = "setup";
            var outName = $"{safeName}-{target}{ext}";
            var outPath = Path.Combine(outDir, outName);
            var manifestJson = JsonSerializer.Serialize(manifest, InstallManifest.JsonOptions);
            var targetFiles = new List<(string EntryName, string FullPath)>(baseFiles);
            AddTargetSpecificFiles(targetFiles, manifest, stubRoot, rid);
            BuildLog.Info($"Packing {targetFiles.Count} file(s) into zip payload…");
            var compressed = await Task.Run(() => PayloadArchive.PackAndCompress(targetFiles, compression, ct), ct);
            BuildLog.Info($"Compressed payload: {BuildLog.FormatBytes(compressed.LongLength)}");

            BuildLog.Info($"Writing installer: {outPath}");
            await using (var stubFs = File.OpenRead(stubPath))
            await using (var outFs = File.Create(outPath))
            {
                await stubFs.CopyToAsync(outFs, ct);
                var mBytes = Encoding.UTF8.GetBytes(manifestJson);
                await outFs.WriteAsync(mBytes, ct);
                await outFs.WriteAsync(compressed, ct);
                InstallPayloadTrailer.WriteFooter(outFs, mBytes.Length, compressed.LongLength);
            }

            var totalSize = new FileInfo(outPath).Length;
            BuildLog.Info($"Built {outPath} ({BuildLog.FormatBytes(totalSize)})");

            if (target.StartsWith("linux-", StringComparison.OrdinalIgnoreCase)
                && string.Equals(manifest.Build.Linux?.Package, "appimage", StringComparison.OrdinalIgnoreCase))
            {
                BuildLog.Info("Packaging AppImage…");
                await AppImagePackager.CreateAsync(outPath, manifest, target, safeName, outDir, baseDirectory, ct);
            }

            if (target.StartsWith("osx-", StringComparison.OrdinalIgnoreCase)
                && string.Equals(manifest.Build.Macos?.Package, "dmg", StringComparison.OrdinalIgnoreCase))
            {
                var dmgOut = Path.Combine(outDir, $"{safeName}-{target}.dmg");
                BuildLog.Info($"Packaging DMG: {dmgOut}");
                DmgPackager.Create(outPath, dmgOut, manifest.Metadata.Name);
            }
        }

        BuildLog.Info("Build finished.");
    }

    /// <summary>
    /// Resolves the stubs root: explicit <paramref name="stubsRoot"/> wins; otherwise a <c>stubs</c> directory next to
    /// the CLI (<see cref="AppContext.BaseDirectory"/>) if present (official release zips); otherwise <c>&lt;base&gt;/stubs</c>.
    /// </summary>
    private static string ResolveStubRoot(string baseDirectory, string? stubsRoot)
    {
        if (!string.IsNullOrWhiteSpace(stubsRoot))
            return Path.GetFullPath(stubsRoot);

        var cliAdjacent = Path.Combine(AppContext.BaseDirectory, "stubs");
        if (Directory.Exists(cliAdjacent))
        {
            BuildLog.VerboseLine($"Using stubs directory next to CLI: {cliAdjacent}");
            return Path.GetFullPath(cliAdjacent);
        }

        var baseStubs = Path.GetFullPath(Path.Combine(baseDirectory, "stubs"));
        BuildLog.VerboseLine($"Using stubs under base directory: {baseStubs}");
        return baseStubs;
    }

    private static string ResolveStubPath(InstallManifest manifest, string stubRoot, string dotnetRid)
    {
        if (!string.IsNullOrWhiteSpace(manifest.Build.StubPath))
        {
            var p = manifest.Build.StubPath.Replace("{rid}", dotnetRid, StringComparison.OrdinalIgnoreCase);
            return Path.GetFullPath(p);
        }

        var isWinTarget = dotnetRid.StartsWith("win-", StringComparison.OrdinalIgnoreCase);
        var ext = isWinTarget ? ".exe" : "";
        return Path.GetFullPath(Path.Combine(stubRoot, dotnetRid, $"PolyInstall.Runtime{ext}"));
    }

    private static void AddTargetSpecificFiles(
        List<(string EntryName, string FullPath)> files,
        InstallManifest manifest,
        string stubRoot,
        string dotnetRid)
    {
        if (!dotnetRid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
            return;
        if (!(manifest.Build.Windows?.RegisterArp ?? true))
            return;

        var uninstallStubPath = ResolveUninstallStubPath(stubRoot, dotnetRid);
        if (!File.Exists(uninstallStubPath))
        {
            throw new FileNotFoundException(
                $"Uninstall stub binary not found for RID {dotnetRid}: {uninstallStubPath}. Publish PolyInstall.Uninstall for this RID into stubs/{dotnetRid}/.");
        }

        var payloadEntry = $"{InstallStatePaths.PolyDirName}/{InstallStatePaths.ToolsDirName}/{InstallStatePaths.UninstallPayloadFileName}";
        if (files.Any(f => string.Equals(f.EntryName, payloadEntry, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Payload already contains reserved path '{payloadEntry}'.");

        files.Add((payloadEntry, uninstallStubPath));
        BuildLog.Info($"Added Windows uninstall stub to payload: {payloadEntry}");
        BuildLog.VerboseLine($"  Source: {uninstallStubPath}");
    }

    private static string ResolveUninstallStubPath(string stubRoot, string dotnetRid)
    {
        const string uninstallExe = "PolyInstall.Uninstall.exe";
        return Path.GetFullPath(Path.Combine(stubRoot, dotnetRid, uninstallExe));
    }

    private static string FindRepoSchema()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "schema", "v1.json");
            if (File.Exists(candidate))
                return Path.GetDirectoryName(candidate)!;
            dir = dir.Parent;
        }
        return Path.Combine(Directory.GetCurrentDirectory(), "schema");
    }
}
