using PolyInstall.Manifest;
using PolyInstall.Conditions;
using PolyInstall.Pal;

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
        || string.Equals(existingInstall?.InstallScope, "machine", StringComparison.OrdinalIgnoreCase)
        || HasActiveWindowsService(manifest)
        || HasVerifiedOwnedSystemService(existingInstall);

    /// <summary>
    /// State-claimed system services cannot by themselves authorize elevation — install
    /// state is user-writable. Elevation is warranted only when at least one claimed
    /// service is verified to have its binary inside the install root.
    /// </summary>
    private static bool HasVerifiedOwnedSystemService(ExistingInstallInfo? existingInstall) =>
        existingInstall is not null
        && existingInstall.State?.RegisteredServices?.Any(s =>
            s.Platform.Equals("windows", StringComparison.OrdinalIgnoreCase)
            && s.Scope.Equals("system", StringComparison.OrdinalIgnoreCase)
            && WindowsServiceOwnership.IsOwnedByInstallRoot(s.Name, existingInstall.InstallLocation)) == true;

    private static bool HasActiveWindowsService(InstallManifest manifest)
    {
        if (manifest.Services is not { Count: > 0 })
            return false;

        return manifest.Services.Any(service =>
            ConditionEvaluator.Evaluate(service.Require)
            && (string.IsNullOrWhiteSpace(service.Scope)
                || service.Scope.Equals("system", StringComparison.OrdinalIgnoreCase)
                || service.Scope.Equals("machine", StringComparison.OrdinalIgnoreCase)));
    }
}
