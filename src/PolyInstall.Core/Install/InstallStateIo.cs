using System.Text.Json;
using PolyInstall.Core.Manifest;

namespace PolyInstall.Core.Install;

public static class InstallStateIo
{
    public static void WriteEmbeddedManifest(string installRoot, InstallManifest manifest)
    {
        var polyDir = InstallStatePaths.PolyDir(installRoot);
        Directory.CreateDirectory(polyDir);
        var path = Path.Combine(polyDir, InstallStatePaths.EmbeddedManifestFileName);
        var json = JsonSerializer.Serialize(manifest, InstallManifest.JsonOptions);
        File.WriteAllText(path, json);
    }

    public static void WriteState(string installRoot, InstallStateDocument state)
    {
        var polyDir = InstallStatePaths.PolyDir(installRoot);
        Directory.CreateDirectory(polyDir);
        var path = Path.Combine(polyDir, InstallStatePaths.InstallStateFileName);
        var json = JsonSerializer.Serialize(state, InstallManifest.JsonOptions);
        File.WriteAllText(path, json);
    }

    public static InstallStateDocument ReadState(string installRoot)
    {
        var path = InstallStatePaths.InstallStatePath(installRoot);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<InstallStateDocument>(json, InstallManifest.JsonOptions)
               ?? throw new InvalidOperationException("Invalid install-state.json.");
    }

    public static InstallManifest ReadEmbeddedManifest(string installRoot)
    {
        var path = InstallStatePaths.EmbeddedManifestPath(installRoot);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<InstallManifest>(json, InstallManifest.JsonOptions)
               ?? throw new InvalidOperationException("Invalid embedded-manifest.json.");
    }
}
