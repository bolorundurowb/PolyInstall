using PolyInstall.Core.Manifest;

namespace PolyInstall.Core.Install;

public static class WindowsElevation
{
    public static bool ShouldRelaunchElevated(
        InstallManifest manifest,
        ExistingInstallInfo? existingInstall,
        bool isWindows,
        bool isAdministrator)
    {
        return isWindows
               && !isAdministrator
               && InstallScopeHelper.RequiresWindowsElevation(manifest, existingInstall);
    }
}
