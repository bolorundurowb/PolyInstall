using PolyInstall.Core.Install;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;
using PolyInstall.Runtime.Pal;

namespace PolyInstall.Runtime.Uninstall;

internal static class UninstallRunner
{
    public static int Run(UninstallCommandLine cmd)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Uninstall is only supported on Windows.");
            return 1;
        }

        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
        {
            Console.Error.WriteLine("Cannot resolve host executable path.");
            return 1;
        }

        var installRoot = ResolveInstallRoot(cmd.InstallLocation, exe);
        if (string.IsNullOrEmpty(installRoot))
        {
            Console.Error.WriteLine(
                "Could not determine installation directory. Use --install-location <path> or run Uninstall.exe from the install folder.");
            return 1;
        }

        if (!Directory.Exists(installRoot))
        {
            Console.Error.WriteLine($"Installation directory does not exist: {installRoot}");
            return 1;
        }

        InstallStateDocument state;
        try
        {
            state = InstallStateIo.ReadState(installRoot);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not read install state: {ex.Message}");
            return 1;
        }

        InstallManifest manifest;
        try
        {
            manifest = InstallStateIo.ReadEmbeddedManifest(installRoot);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not read embedded manifest: {ex.Message}");
            return 1;
        }

        if (!cmd.Quiet && !WindowsUninstallPrompt.Confirm(state.DisplayName))
            return 1;

        var pal = new DefaultPolyInstallPal();
        UninstallCoordinator.Run(state, manifest, pal, exe);
        return 0;
    }

    private static string? ResolveInstallRoot(string? fromArgs, string hostExe)
    {
        if (!string.IsNullOrWhiteSpace(fromArgs))
            return Path.GetFullPath(fromArgs.Trim());

        var name = Path.GetFileName(hostExe);
        if (name.Equals(InstallStatePaths.UninstallExeFileName, StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(hostExe);

        return null;
    }
}
