using System.Runtime.InteropServices;
using Avalonia;
using PolyInstall.Hosting;
using PolyInstall.UI;

namespace PolyInstall.Runtime;

internal static class Program
{
    private const int AttachParentProcess = -1;

    [STAThread]
    private static int Main(string[] args)
    {
        var silentRequested = args.Any(a =>
            a.Equals("--silent", StringComparison.OrdinalIgnoreCase)
            || a.Equals("/S", StringComparison.OrdinalIgnoreCase)
            || a.Equals("/silent", StringComparison.OrdinalIgnoreCase)
            || a.Equals("--help", StringComparison.OrdinalIgnoreCase)
            || a.Equals("-h", StringComparison.OrdinalIgnoreCase));

        if (silentRequested)
            return RunSilent(args);

        return RunWizard(args);
    }

    private static int RunSilent(string[] args)
    {
        TryAttachParentConsole();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        PosixSignalRegistration? sigTerm = null;
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                sigTerm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => cts.Cancel());
            }
            catch
            {
                // Best-effort
            }
        }

        try
        {
            using var session = InstallerHost.Open();
            return InstallerHost.RunSilent(session, args, cts.Token);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return SilentExitCodes.UnexpectedError;
        }
        finally
        {
            sigTerm?.Dispose();
        }
    }

    private static int RunWizard(string[] args)
    {
        try
        {
            var probe = InstallerHost.Probe();
            if (InstallerHost.TryRelaunchElevatedForWizard(probe, args))
                return 0;

            using var session = InstallerHost.Open(probe);
            InstallBootstrap.Init(session.Manifest, session.ExtractRoot, session.Pal, session.ExistingInstall);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                // ANGLE natives are excluded from the publish (see PolyInstall.Runtime.csproj);
                // software rendering is sufficient for the installer wizard.
                RenderingMode = [Win32RenderingMode.Software],
            })
            .LogToTrace();

    private static void TryAttachParentConsole()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
#pragma warning disable CA1416
            AttachConsole(AttachParentProcess);
#pragma warning restore CA1416
        }
        catch
        {
            // No parent console (double-click); progress still goes to --log when supplied.
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern bool AttachConsole(int dwProcessId);
}
