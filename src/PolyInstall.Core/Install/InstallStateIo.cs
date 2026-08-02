using System.Text.Json;
using PolyInstall.Manifest;
using InstallJsonContext = PolyInstall.Manifest.InstallJsonContext;

namespace PolyInstall.Install;

public static class InstallStateIo
{
    public static void WriteEmbeddedManifest(string installRoot, InstallManifest manifest)
    {
        var polyDir = InstallStatePaths.PolyDir(installRoot);
        Directory.CreateDirectory(polyDir);
        var path = Path.Combine(polyDir, InstallStatePaths.EmbeddedManifestFileName);
        var json = JsonSerializer.Serialize(manifest, InstallJsonContext.Default.InstallManifest);
        File.WriteAllText(path, json);
    }

    public static void WriteState(string installRoot, InstallStateDocument state)
    {
        var polyDir = InstallStatePaths.PolyDir(installRoot);
        Directory.CreateDirectory(polyDir);
        var path = Path.Combine(polyDir, InstallStatePaths.InstallStateFileName);
        var json = JsonSerializer.Serialize(state, InstallJsonContext.Default.InstallStateDocument);
        File.WriteAllText(path, json);
    }

    public static InstallStateDocument ReadState(string installRoot)
    {
        var path = InstallStatePaths.InstallStatePath(installRoot);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize(json, InstallJsonContext.Default.InstallStateDocument)
               ?? throw new InvalidOperationException("Invalid install-state.json.");
    }

    public static InstallManifest ReadEmbeddedManifest(string installRoot)
    {
        var path = InstallStatePaths.EmbeddedManifestPath(installRoot);
        var json = File.ReadAllText(path);
        var manifest = JsonSerializer.Deserialize(json, InstallJsonContext.Default.InstallManifest)
                       ?? throw new InvalidOperationException("Invalid embedded-manifest.json.");
        RuntimeManifestGuard.Validate(manifest);
        return manifest;
    }
}
