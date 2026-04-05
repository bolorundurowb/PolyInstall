using System.Text;
using System.Text.Json;
using PolyInstall.Core.Build;
using PolyInstall.Core.Globbing;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Payload;
using PolyInstall.Cli.Validation;

namespace PolyInstall.Cli.Build;

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

        var allFiles = new List<(string EntryName, string FullPath)>();
        foreach (var entry in manifest.Files)
        {
            var globs = GlobResolver.Collect(baseDirectory, entry.SourceDir, entry.Include, entry.Exclude);
            foreach (var g in globs)
                allFiles.Add((g.RelativePath, g.FullPath));
        }

        if (allFiles.Count == 0)
            throw new InvalidOperationException("No files matched manifest files entries; nothing to pack.");

        var compression = PayloadArchive.ParseCompression(manifest.Build.Compression);
        var compressed = await Task.Run(() => PayloadArchive.PackAndCompress(allFiles, compression, ct), ct);
        var manifestJson = JsonSerializer.Serialize(manifest, InstallManifest.JsonOptions);

        var outDir = Path.GetFullPath(Path.Combine(baseDirectory, manifest.Build.OutputDir));
        Directory.CreateDirectory(outDir);

        var stubRoot = stubsRoot ?? Path.Combine(baseDirectory, "stubs");
        foreach (var target in manifest.Build.Targets)
        {
            ct.ThrowIfCancellationRequested();
            var rid = RidMapping.ToDotNetRid(target);
            var stubPath = ResolveStubPath(manifest, stubRoot, rid);
            if (!File.Exists(stubPath))
                throw new FileNotFoundException($"Stub binary not found for target '{target}' (RID {rid}): {stubPath}. Publish PolyInstall.Runtime for this RID into stubs/{rid}/.");

            var ext = OperatingSystem.IsWindows() ? ".exe" : "";
            var safeName = string.Join("_", manifest.Metadata.Name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (string.IsNullOrEmpty(safeName))
                safeName = "setup";
            var outName = $"{safeName}-{target}{ext}";
            var outPath = Path.Combine(outDir, outName);

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

    private static string ResolveStubPath(InstallManifest manifest, string stubRoot, string dotnetRid)
    {
        if (!string.IsNullOrWhiteSpace(manifest.Build.StubPath))
        {
            var p = manifest.Build.StubPath.Replace("{rid}", dotnetRid, StringComparison.OrdinalIgnoreCase);
            return Path.GetFullPath(p);
        }

        var ext = OperatingSystem.IsWindows() ? ".exe" : "";
        return Path.GetFullPath(Path.Combine(stubRoot, dotnetRid, $"PolyInstall.Runtime{ext}"));
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
