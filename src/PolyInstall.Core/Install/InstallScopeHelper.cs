using PolyInstall.Manifest;
using PolyInstall.Conditions;

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
        || existingInstall?.State?.RegisteredServices?.Any(s =>
            s.Platform.Equals("windows", StringComparison.OrdinalIgnoreCase)
            && s.Scope.Equals("system", StringComparison.OrdinalIgnoreCase)) == true;

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
