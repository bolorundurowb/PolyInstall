using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PolyInstall.Core.Install;

public static class WindowsArpRegistration
{
    [SupportedOSPlatform("windows")]
    public static void Register(InstallStateDocument state, string uninstallExePath, long estimatedSizeKib)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var key = OpenUninstallKey(state, writable: true)
                        ?? throw new InvalidOperationException($"Could not create ARP registry key: {state.RegistryUninstallKeyRelative}.");

        key.SetValue("DisplayName", state.DisplayName);
        key.SetValue("DisplayVersion", state.DisplayVersion);
        if (!string.IsNullOrEmpty(state.Publisher))
            key.SetValue("Publisher", state.Publisher!);
        key.SetValue("InstallLocation", state.InstallLocation);
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));

        var unquoted = $"\"{uninstallExePath}\" --uninstall";
        key.SetValue("UninstallString", unquoted);
        key.SetValue("QuietUninstallString", $"{unquoted} --quiet");
        key.SetValue("DisplayIcon", $"{uninstallExePath},0");

        if (estimatedSizeKib > int.MaxValue)
            estimatedSizeKib = int.MaxValue;
        key.SetValue("EstimatedSize", (int)estimatedSizeKib, RegistryValueKind.DWord);
    }

    [SupportedOSPlatform("windows")]
    public static void Unregister(InstallStateDocument state)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var root = GetHive(state);
            root.DeleteSubKeyTree(state.RegistryUninstallKeyRelative, throwOnMissingSubKey: false);
        }
        catch
        {
        }
    }

    [SupportedOSPlatform("windows")]
    private static RegistryKey? OpenUninstallKey(InstallStateDocument state, bool writable)
    {
        var root = GetHive(state);
        return root.CreateSubKey(state.RegistryUninstallKeyRelative, writable);
    }

    [SupportedOSPlatform("windows")]
    private static RegistryKey GetHive(InstallStateDocument state) =>
        state.InstallScope.Equals("machine", StringComparison.OrdinalIgnoreCase)
            ? Registry.LocalMachine
            : Registry.CurrentUser;
}
