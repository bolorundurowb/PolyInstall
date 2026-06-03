using PolyInstall.Core.Install;
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
    public static ExistingInstallInfo? ExistingInstall { get; set; }
    public static InstallMode SelectedInstallMode { get; set; } = InstallMode.Install;
    public static string? InstallDirectory { get; set; }

    public static void Init(
        InstallManifest manifest,
        string extractRoot,
        IPolyInstallPal pal,
        ExistingInstallInfo? existingInstall = null)
    {
        Manifest = manifest;
        ExtractRoot = extractRoot;
        Pal = pal;
        ExistingInstall = existingInstall;
        SelectedInstallMode = InstallCoordinator.ResolveMode(manifest, existingInstall);
        InstallDirectory = null;
        if (existingInstall is not null)
            InstallDirectory = existingInstall.InstallLocation;
    }
}
