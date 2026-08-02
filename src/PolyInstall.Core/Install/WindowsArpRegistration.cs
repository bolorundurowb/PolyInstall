using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PolyInstall.Install;

public static class WindowsArpRegistration
{
    public static string RegistryKeyRelativeForProductId(string productId) =>
        $@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{productId}";

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
    public static InstallStateDocument? TryRead(string productId, string installScope)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        var relativeKey = RegistryKeyRelativeForProductId(productId);
        var root = installScope.Equals("machine", StringComparison.OrdinalIgnoreCase)
            ? Registry.LocalMachine
            : Registry.CurrentUser;
        using var key = root.OpenSubKey(relativeKey, writable: false);
        if (key is null)
            return null;

        var installLocation = key.GetValue("InstallLocation") as string;
        if (string.IsNullOrWhiteSpace(installLocation))
            return null;

        return new InstallStateDocument
        {
            ProductId = productId,
            DisplayName = key.GetValue("DisplayName") as string ?? "",
            DisplayVersion = key.GetValue("DisplayVersion") as string ?? "",
            Publisher = key.GetValue("Publisher") as string,
            InstallLocation = installLocation,
            InstallScope = installScope,
            RegistryUninstallKeyRelative = relativeKey,
        };
    }

    [SupportedOSPlatform("windows")]
    public static void Unregister(InstallStateDocument state, string? expectedProductId = null)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            // The product id binds the deleted key to the manifest being uninstalled; the
            // relative key is recomputed from it instead of trusting the (user-writable)
            // state value RegistryUninstallKeyRelative.
            if (expectedProductId is not null
                && !state.ProductId.Equals(expectedProductId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var relativeKey = RegistryKeyRelativeForProductId(state.ProductId);
            var root = GetHive(state);
            root.DeleteSubKeyTree(relativeKey, throwOnMissingSubKey: false);
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
