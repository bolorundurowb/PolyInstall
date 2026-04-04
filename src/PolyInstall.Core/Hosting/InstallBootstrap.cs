using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;

namespace PolyInstall.Core.Hosting;

/// <summary>
/// Populated by the runtime host before Avalonia starts; read by the UI layer.
/// </summary>
public static class InstallBootstrap
{
    public static InstallManifest Manifest { get; private set; } = null!;
    public static string ExtractRoot { get; private set; } = "";
    public static IPolyInstallPal Pal { get; private set; } = null!;
    public static string? InstallDirectory { get; set; }

    public static void Init(InstallManifest manifest, string extractRoot, IPolyInstallPal pal)
    {
        Manifest = manifest;
        ExtractRoot = extractRoot;
        Pal = pal;
        InstallDirectory = null;
    }
}
