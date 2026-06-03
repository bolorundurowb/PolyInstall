using System.Runtime.Versioning;
using System.Security.Principal;
using PolyInstall.Manifest;

namespace PolyInstall.Install;

public static class InstallFinalizer
{
    public static InstallStateDocument FinalizeInstall(
        InstallManifest manifest,
        string installDirectory,
        IReadOnlyCollection<string> payloadFiles)
    {
        var state = CreateState(manifest, installDirectory, payloadFiles);

        InstallStateIo.WriteEmbeddedManifest(installDirectory, manifest);
        InstallStateIo.WriteState(installDirectory, state);

        if (OperatingSystem.IsWindows())
            FinalizeWindowsInstall(manifest, installDirectory, state);

        return state;
    }

    public static InstallStateDocument CreateState(
        InstallManifest manifest,
        string installDirectory,
        IReadOnlyCollection<string> payloadFiles)
    {
        var productId = ProductIdHelper.StableProductGuidString(manifest.Metadata);
        return new InstallStateDocument
        {
            ProductId = productId,
            DisplayName = manifest.Metadata.Name,
            DisplayVersion = manifest.Metadata.Version,
            Publisher = manifest.Metadata.Publisher,
            InstallLocation = installDirectory,
            InstallScope = InstallScopeHelper.GetInstallScope(manifest),
            RegistryUninstallKeyRelative = WindowsArpRegistration.RegistryKeyRelativeForProductId(productId),
            PayloadFiles = payloadFiles.Order(StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }

    [SupportedOSPlatform("windows")]
    private static void FinalizeWindowsInstall(
        InstallManifest manifest,
        string installDirectory,
        InstallStateDocument state)
    {
        var win = manifest.Build.Windows ?? new WindowsBuildOptions();
        if (!win.RegisterArp)
            return;

        if (InstallScopeHelper.IsMachineInstall(manifest) && !IsWindowsAdministrator())
        {
            throw new InvalidOperationException(
                "Per-machine installs require Administrator rights for Add/Remove Programs registration. Use install_scope: user or run the installer elevated.");
        }

        var bundledUninstallPath = InstallStatePaths.UninstallPayloadPath(installDirectory);
        if (!File.Exists(bundledUninstallPath))
        {
            throw new InvalidOperationException(
                $"Bundled uninstaller not found at '{bundledUninstallPath}'. Publish PolyInstall.Uninstall into stubs for this target before building installers.");
        }

        var uninstallPath = InstallStatePaths.UninstallExePath(installDirectory);
        File.Copy(bundledUninstallPath, uninstallPath, overwrite: true);

        var estimatedKb = InstallDirectoryEstimator.EstimateKibRecursive(installDirectory);
        WindowsArpRegistration.Register(state, uninstallPath, estimatedKb);
    }

    private static bool IsWindowsAdministrator()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        using var wi = WindowsIdentity.GetCurrent();
        var wp = new WindowsPrincipal(wi);
        return wp.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
