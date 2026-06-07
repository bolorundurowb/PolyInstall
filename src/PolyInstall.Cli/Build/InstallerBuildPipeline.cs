using System.Text;
using System.Text.Json;
using PolyInstall.Build;
using PolyInstall.Cli.Validation;
using PolyInstall.Core.Build.Globbing;
using PolyInstall.Core.Build.Manifest;
using PolyInstall.Core.Build.Validation;
using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Payload;

namespace PolyInstall.Cli.Build;

/// <summary>
/// Builds self-extracting installers by appending manifest + compressed payload to a pre-published
/// PolyInstall.Runtime stub binary. Windows installers with Add/Remove Programs registration enabled also embed
/// <c>PolyInstall.Uninstall.exe</c> under <c>.polyinstall/tools/</c> so the installer can copy it to
/// <c>Uninstall.exe</c>.
/// </summary>
public static class InstallerBuildPipeline
{
    public static async Task<BuildOutputManifest> RunAsync(
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
        BuildLog.Info("Manifest schema validation passed.");

        BuildLog.Info("Running manifest semantic validation…");
        ManifestSemanticValidator.Validate(manifest);
        BuildLog.Info("Manifest semantic validation passed.");

        BuildLog.Info("Collecting files from manifest entries…");
        var baseFiles = new List<(string EntryName, string FullPath)>();
        var featureIndex = manifest.Features is { Count: > 0 } ? new PayloadFeatureIndex() : null;
        var seenCore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPerFeature = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in manifest.Files)
        {
            var globs = GlobResolver.Collect(baseDirectory, entry.SourceDir, entry.Include, entry.Exclude);
            var excludeList = entry.Exclude is { } ex ? string.Join(", ", ex) : "(none)";
            var entryFeatures = entry.Features is { Count: > 0 } ? entry.Features : null;
            var featuresLabel = entryFeatures is null ? "(core)" : string.Join(",", entryFeatures);
            BuildLog.VerboseLine(
                $"  {entry.SourceDir}: {globs.Count} file(s) (include: {string.Join(", ", entry.Include)}; exclude: {excludeList}; features: {featuresLabel})");
            foreach (var g in globs)
            {
                baseFiles.Add((g.RelativePath, g.FullPath));
                if (featureIndex is null)
                    continue;
                if (entryFeatures is null)
                {
                    if (seenCore.Add(g.RelativePath))
                        featureIndex.CoreFiles.Add(g.RelativePath);
                }
                else
                {
                    foreach (var featureId in entryFeatures)
                    {
                        if (!seenPerFeature.TryGetValue(featureId, out var set))
                            seenPerFeature[featureId] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (!featureIndex.FeatureFiles.TryGetValue(featureId, out var list))
                            featureIndex.FeatureFiles[featureId] = list = new List<string>();
                        if (set.Add(g.RelativePath))
                            list.Add(g.RelativePath);
                    }
                }
            }
        }

        if (baseFiles.Count == 0)
            throw new InvalidOperationException("No files matched manifest files entries; nothing to pack.");

        if (featureIndex is not null)
        {
            featureIndex.CoreFiles.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var list in featureIndex.FeatureFiles.Values)
                list.Sort(StringComparer.OrdinalIgnoreCase);
            manifest.FeatureIndex = featureIndex;
            BuildLog.Info(
                $"Payload feature index: {featureIndex.CoreFiles.Count} core file(s), " +
                $"{featureIndex.FeatureFiles.Count} feature group(s) " +
                $"[{string.Join(", ", featureIndex.FeatureFiles.Select(kv => $"{kv.Key}={kv.Value.Count}"))}].");
        }

        BuildLog.Info($"Collected {baseFiles.Count} file(s) for the payload.");

        var compression = PayloadArchive.ParseCompression(manifest.Build.Compression);
        BuildLog.Info($"Payload compression: {compression}");

        var outDir = Path.GetFullPath(Path.Combine(baseDirectory, manifest.Build.OutputDir));
        Directory.CreateDirectory(outDir);
        BuildLog.Info($"Output directory: {outDir}");

        var stubRoot = ResolveStubRoot(baseDirectory, stubsRoot);
        BuildLog.Info($"Stubs root: {stubRoot}");
        BuildLog.Info($"Build targets: {string.Join(", ", manifest.Build.Targets)}");

        var artifacts = new List<BuildArtifact>();

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
            var sanitizedProductName = SanitizeProductName(manifest.Metadata.Name);
            var outName = ResolveOutputName(manifest, target, ext);
            var outPath = Path.Combine(outDir, outName);
            var manifestJson = JsonSerializer.Serialize(manifest, InstallManifest.JsonOptions);
            var targetFiles = new List<(string EntryName, string FullPath)>(baseFiles);
            var temporaryPaths = new List<string>();
            try
            {
                await AddTargetSpecificFilesAsync(targetFiles, manifest, stubRoot, rid, temporaryPaths, ct);
                BuildLog.Info($"Packing {targetFiles.Count} file(s) into zip payload…");
                var compressedPayloadPath = Path.Combine(Path.GetTempPath(), "polyinstall-payload-" + Guid.NewGuid().ToString("n") + ".bin");
                temporaryPaths.Add(compressedPayloadPath);
                var compressedPayloadLength = await Task.Run(
                    () => PayloadArchive.PackAndCompressToFile(targetFiles, compression, compressedPayloadPath, ct),
                    ct);
                BuildLog.Info($"Compressed payload: {BuildLog.FormatBytes(compressedPayloadLength)}");

                BuildLog.Info($"Writing installer: {outPath}");
                await using (var stubFs = File.OpenRead(stubPath))
                await using (var payloadFs = File.OpenRead(compressedPayloadPath))
                await using (var outFs = File.Create(outPath))
                {
                    await stubFs.CopyToAsync(outFs, ct);
                    var mBytes = Encoding.UTF8.GetBytes(manifestJson);
                    await outFs.WriteAsync(mBytes, ct);
                    await payloadFs.CopyToAsync(outFs, ct);
                    InstallPayloadTrailer.WriteFooter(outFs, mBytes.Length, compressedPayloadLength);
                }

                var totalSize = new FileInfo(outPath).Length;
                BuildLog.Info($"Built {outPath} ({BuildLog.FormatBytes(totalSize)})");

                if (isWinTarget && manifest.Build.Signing?.Windows is { } windowsSigning)
                {
                    BuildLog.Info("Signing Windows installer…");
                    await InstallerSigner.SignWindowsAsync(outPath, windowsSigning, ct);
                    BuildLog.Info("Signed Windows installer.");
                }

                if (target.StartsWith("osx-", StringComparison.OrdinalIgnoreCase)
                    && manifest.Build.Signing?.Macos is { } macOsSigning)
                {
                    BuildLog.Info("Signing macOS installer executable…");
                    await InstallerSigner.SignMacOsExecutableAsync(outPath, macOsSigning, ct);
                    BuildLog.Info("Signed macOS installer executable.");
                }

                var finalInstallerSize = new FileInfo(outPath).Length;
                artifacts.Add(new BuildArtifact(target, rid, "installer", outPath, finalInstallerSize));

                if (target.StartsWith("linux-", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(manifest.Build.Linux?.Package, "appimage", StringComparison.OrdinalIgnoreCase))
                {
                    BuildLog.Info("Packaging AppImage…");
                    var appImagePath = await AppImagePackager.CreateAsync(outPath, manifest, target, sanitizedProductName, outDir, baseDirectory, ct);
                    var appImageSize = new FileInfo(appImagePath).Length;
                    artifacts.Add(new BuildArtifact(target, rid, "appimage", appImagePath, appImageSize));
                }

                if (target.StartsWith("osx-", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(manifest.Build.Macos?.Package, "dmg", StringComparison.OrdinalIgnoreCase))
                {
                    var dmgOut = Path.Combine(outDir, $"{sanitizedProductName}-{target}.dmg");
                    BuildLog.Info($"Packaging DMG: {dmgOut}");
                    var dmgPath = DmgPackager.Create(outPath, dmgOut, manifest.Metadata.Name);
                    if (manifest.Build.Signing?.Macos is { } macOsDmgSigning)
                    {
                        BuildLog.Info("Signing macOS DMG…");
                        await InstallerSigner.SignMacOsDmgAsync(dmgPath, macOsDmgSigning, ct);
                        BuildLog.Info("Signed macOS DMG.");
                    }
                    var dmgSize = new FileInfo(dmgPath).Length;
                    artifacts.Add(new BuildArtifact(target, rid, "dmg", dmgPath, dmgSize));
                }
            }
            finally
            {
                foreach (var path in temporaryPaths)
                    TryDelete(path);
            }
        }

        BuildLog.Info("Build finished.");
        return new BuildOutputManifest(manifest.Metadata.Name, manifest.Metadata.Version, artifacts);
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

    private static async Task AddTargetSpecificFilesAsync(
        List<(string EntryName, string FullPath)> files,
        InstallManifest manifest,
        string stubRoot,
        string dotnetRid,
        List<string> temporaryPaths,
        CancellationToken ct)
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

        var payloadSourcePath = uninstallStubPath;
        if (manifest.Build.Signing?.Windows is { } signing)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "polyinstall-sign-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(tempDir);
            temporaryPaths.Add(tempDir);
            payloadSourcePath = Path.Combine(tempDir, Path.GetFileName(uninstallStubPath));
            File.Copy(uninstallStubPath, payloadSourcePath, overwrite: true);
            BuildLog.Info("Signing Windows uninstall stub before embedding…");
            await InstallerSigner.SignWindowsAsync(payloadSourcePath, signing, ct);
            BuildLog.Info("Signed Windows uninstall stub.");
        }

        var payloadEntry = $"{InstallStatePaths.PolyDirName}/{InstallStatePaths.ToolsDirName}/{InstallStatePaths.UninstallPayloadFileName}";
        if (files.Any(f => string.Equals(f.EntryName, payloadEntry, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Payload already contains reserved path '{payloadEntry}'.");

        files.Add((payloadEntry, payloadSourcePath));
        BuildLog.Info($"Added Windows uninstall stub to payload: {payloadEntry}");
        BuildLog.VerboseLine($"  Source: {payloadSourcePath}");
    }

    private static string ResolveUninstallStubPath(string stubRoot, string dotnetRid)
    {
        const string uninstallExe = "PolyInstall.Uninstall.exe";
        return Path.GetFullPath(Path.Combine(stubRoot, dotnetRid, uninstallExe));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
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

    private static string ResolveOutputName(InstallManifest manifest, string target, string extension)
    {
        var pattern = manifest.Build.OutputName;
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            var resolved = pattern!
                .Replace("{name}", manifest.Metadata.Name, StringComparison.OrdinalIgnoreCase)
                .Replace("{version}", manifest.Metadata.Version, StringComparison.OrdinalIgnoreCase)
                .Replace("{target}", target, StringComparison.OrdinalIgnoreCase);
            var sanitized = string.Join("_", resolved.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (string.IsNullOrEmpty(sanitized))
                sanitized = "setup";
            if (!sanitized.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && extension.Length > 0)
                sanitized += extension;
            return sanitized;
        }

        var safeName = SanitizeProductName(manifest.Metadata.Name);
        return $"{safeName}-{target}{extension}";
    }

    private static string SanitizeProductName(string name)
    {
        var safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        return string.IsNullOrEmpty(safeName) ? "setup" : safeName;
    }
}
