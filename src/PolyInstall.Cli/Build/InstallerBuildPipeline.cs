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
public sealed class InstallerBuildPipeline
{
    public static async Task RunAsync(
        string manifestPath,
        string baseDirectory,
        string? stubsRoot,
        CancellationToken ct)
    {
        baseDirectory = Path.GetFullPath(baseDirectory);
        var yaml = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = ManifestYaml.Parse(yaml);
        manifest = EnvironmentSubstitution.ApplyToManifest(manifest);

        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schema", "v1.json");
        if (!File.Exists(schemaPath))
            schemaPath = Path.Combine(FindRepoSchema(), "v1.json");
        var json = JsonSerializer.Serialize(manifest, InstallManifest.JsonOptions);
        ManifestJsonValidator.Validate(json, schemaPath);

        var baseFiles = new List<(string EntryName, string FullPath)>();
        foreach (var entry in manifest.Files)
        {
            var globs = GlobResolver.Collect(baseDirectory, entry.SourceDir, entry.Include, entry.Exclude);
            foreach (var g in globs)
                baseFiles.Add((g.RelativePath, g.FullPath));
        }

        if (baseFiles.Count == 0)
            throw new InvalidOperationException("No files matched manifest files entries; nothing to pack.");

        var compression = PayloadArchive.ParseCompression(manifest.Build.Compression);

        var outDir = Path.GetFullPath(Path.Combine(baseDirectory, manifest.Build.OutputDir));
        Directory.CreateDirectory(outDir);

        var stubRoot = ResolveStubRoot(baseDirectory, stubsRoot);
        foreach (var target in manifest.Build.Targets)
        {
            ct.ThrowIfCancellationRequested();
            manifest.Build.InstallerTarget = target;
            var rid = RidMapping.ToDotNetRid(target);
            var stubPath = ResolveStubPath(manifest, stubRoot, rid);
            if (!File.Exists(stubPath))
                throw new FileNotFoundException($"Stub binary not found for target '{target}' (RID {rid}): {stubPath}. Publish PolyInstall.Runtime for this RID into stubs/{rid}/.");

            var isWinTarget = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase);
            var ext = isWinTarget ? ".exe" : "";
            var safeName = string.Join("_", manifest.Metadata.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (string.IsNullOrEmpty(safeName))
                safeName = "setup";
            var outName = $"{safeName}-{target}{ext}";
            var outPath = Path.Combine(outDir, outName);
            var manifestJson = JsonSerializer.Serialize(manifest, InstallManifest.JsonOptions);
            var targetFiles = new List<(string EntryName, string FullPath)>(baseFiles);
            AddTargetSpecificFiles(targetFiles, stubRoot, rid);
            var compressed = await Task.Run(() => PayloadArchive.PackAndCompress(targetFiles, compression, ct), ct);

            await using (var stubFs = File.OpenRead(stubPath))
            await using (var outFs = File.Create(outPath))
            {
                await stubFs.CopyToAsync(outFs, ct);
                var mBytes = Encoding.UTF8.GetBytes(manifestJson);
                await outFs.WriteAsync(mBytes, ct);
                await outFs.WriteAsync(compressed, ct);
                InstallPayloadTrailer.WriteFooter(outFs, mBytes.Length, compressed.LongLength);
            }

            Console.WriteLine($"Built {outPath}");

            if (target.StartsWith("linux-", StringComparison.OrdinalIgnoreCase)
                && string.Equals(manifest.Build.Linux?.Package, "appimage", StringComparison.OrdinalIgnoreCase))
            {
                await AppImagePackager.CreateAsync(outPath, manifest, target, safeName, outDir, baseDirectory, ct);
            }

            if (target.StartsWith("osx-", StringComparison.OrdinalIgnoreCase)
                && string.Equals(manifest.Build.Macos?.Package, "dmg", StringComparison.OrdinalIgnoreCase))
            {
                var dmgOut = Path.Combine(outDir, $"{safeName}-{target}.dmg");
                DmgPackager.Create(outPath, dmgOut, manifest.Metadata.Name);
            }
        }
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
            return Path.GetFullPath(cliAdjacent);

        return Path.GetFullPath(Path.Combine(baseDirectory, "stubs"));
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
        string stubRoot,
        string dotnetRid)
    {
        if (!dotnetRid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
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
