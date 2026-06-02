using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using Avalonia;
using PolyInstall.Core.Hosting;
using PolyInstall.Core.Install;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;
using PolyInstall.Core.Payload;
using PolyInstall.UI;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot resolve host executable path.");
        var (manifest, compressed) = InstallBundleReader.ReadFromSeekableFile(exe);
        if (RelaunchElevatedForMachineInstall(exe, args, manifest))
            return;

        var raw = InstallBundleReader.DecompressPayload(manifest, compressed);
        var extract = Path.Combine(Path.GetTempPath(), "polyinstall-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(extract);
        ZipPayloadExtractor.ExtractToDirectory(raw, extract);
        var pal = new DefaultPolyInstallPal();
        InstallBootstrap.Init(manifest, extract, pal);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static bool RelaunchElevatedForMachineInstall(string exe, string[] args, InstallManifest manifest)
    {
        if (!OperatingSystem.IsWindows() || !IsMachineInstall(manifest) || IsWindowsAdministrator())
            return false;

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

    private static bool IsMachineInstall(InstallManifest manifest)
    {
        var scope = manifest.Build.Windows?.InstallScope;
        return string.Equals(scope?.Trim(), "machine", StringComparison.OrdinalIgnoreCase);
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
