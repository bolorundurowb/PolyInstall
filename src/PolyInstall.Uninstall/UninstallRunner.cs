using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using PolyInstall.Manifest;
using PolyInstall.Install;
using PolyInstall.Pal;

namespace PolyInstall.Uninstall;

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

        if (ShouldRelaunchElevated(state, installRoot) && RelaunchElevated(exe, Environment.GetCommandLineArgs().Skip(1)))
            return 0;

        if (!cmd.Quiet && !WindowsUninstallPrompt.Confirm(state.DisplayName))
            return 1;

        var pal = new DefaultPolyInstallPal();
        UninstallCoordinator.Run(state, manifest, pal, exe, installRoot);
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

    private static bool ShouldRelaunchElevated(InstallStateDocument state, string installRoot)
    {
        // install-state.json is user-writable, so it cannot by itself authorize elevation.
        // Elevation is only requested when at least one claimed system service is verified
        // to have its binary inside this install root (i.e. it was really installed by us).
        return OperatingSystem.IsWindows()
               && state.RegisteredServices?.Any(s =>
                   s.Platform.Equals("windows", StringComparison.OrdinalIgnoreCase)
                   && s.Scope.Equals("system", StringComparison.OrdinalIgnoreCase)
                   && WindowsServiceOwnership.IsOwnedByInstallRoot(s.Name, installRoot)) == true
               && !IsWindowsAdministrator();
    }

    private static bool RelaunchElevated(string exe, IEnumerable<string> args)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = JoinArguments(args),
                UseShellExecute = true,
                Verb = "runas",
            });
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsWindowsAdministrator()
    {
        using var wi = WindowsIdentity.GetCurrent();
        var wp = new WindowsPrincipal(wi);
        return wp.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static string JoinArguments(IEnumerable<string> args) => string.Join(" ", args.Select(QuoteArgument));

    private static string QuoteArgument(string arg)
    {
        if (arg.Length == 0)
            return "\"\"";
        if (!arg.Any(char.IsWhiteSpace) && !arg.Contains('"'))
            return arg;

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
