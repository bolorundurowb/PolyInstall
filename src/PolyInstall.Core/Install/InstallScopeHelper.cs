using PolyInstall.Manifest;

namespace PolyInstall.Install;

public static class InstallScopeHelper
{
    public static string GetInstallScope(InstallManifest manifest)
    {
        var scope = manifest.Build.Windows?.InstallScope;
        return string.IsNullOrWhiteSpace(scope) ? "user" : scope.Trim();
    }

    public static bool IsMachineInstall(InstallManifest manifest) =>
        GetInstallScope(manifest).Equals("machine", StringComparison.OrdinalIgnoreCase);

    public static bool RequiresWindowsElevation(InstallManifest manifest, ExistingInstallInfo? existingInstall) =>
        IsMachineInstall(manifest)
        || string.Equals(existingInstall?.InstallScope, "machine", StringComparison.OrdinalIgnoreCase);
}
