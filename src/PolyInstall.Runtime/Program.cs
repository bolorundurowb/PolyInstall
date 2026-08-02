using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using Avalonia;
using PolyInstall.Hosting;
using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;
using PolyInstall.Payload;
using PolyInstall.UI;

namespace PolyInstall.Runtime;

internal static class Program
{
    /// <summary>
    /// Entry point for the self-extracting installer.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve host executable path.");
        var manifest = InstallBundleReader.ReadManifestFromSeekableFile(exe);
        var pal = new DefaultPolyInstallPal();
        var existingInstall = InstalledProductLocator.Find(manifest, pal);
        if (RelaunchElevatedForMachineInstall(exe, args, manifest, existingInstall))
            return;

        var extract = Path.Combine(Path.GetTempPath(), "polyinstall-" + Guid.NewGuid().ToString("n"));
        var zipPath = Path.Combine(Path.GetTempPath(), "polyinstall-payload-" + Guid.NewGuid().ToString("n") + ".zip");
        try
        {
            Directory.CreateDirectory(extract);
            InstallBundleReader.DecompressPayloadToFile(exe, manifest, zipPath);
            ZipPayloadExtractor.ExtractFileToDirectory(zipPath, extract);
            InstallBootstrap.Init(manifest, extract, pal, existingInstall);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            TryDeleteFile(zipPath);
            TryDeleteDirectory(extract);
        }
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    /// <summary>
    /// Attempts to re-launch the process with elevated privileges if required for machine-wide installation on Windows.
    /// </summary>
    private static bool RelaunchElevatedForMachineInstall(
        string exe,
        string[] args,
        InstallManifest manifest,
        ExistingInstallInfo? existingInstall)
    {
        if (!WindowsElevation.ShouldRelaunchElevated(
                manifest,
                existingInstall,
                OperatingSystem.IsWindows(),
                OperatingSystem.IsWindows() && IsWindowsAdministrator()))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = JoinArguments(args),
                UseShellExecute = true,
                Verb = "runas",
            });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // UAC prompt was cancelled; leave the non-elevated process instead of continuing to a doomed install.
        }

        return true;
    }

    /// <summary>
    /// Checks if the current process is running with Windows Administrator privileges.
    /// </summary>
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

        var quoted = new System.Text.StringBuilder();
        quoted.Append('"');
        var pendingBackslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                pendingBackslashes++;
                continue;
            }

            if (c == '"')
            {
                quoted.Append('\\', pendingBackslashes * 2 + 1);
                quoted.Append('"');
            }
            else
            {
                quoted.Append('\\', pendingBackslashes);
                quoted.Append(c);
            }

            pendingBackslashes = 0;
        }

        quoted.Append('\\', pendingBackslashes * 2);
        quoted.Append('"');
        return quoted.ToString();
    }
}