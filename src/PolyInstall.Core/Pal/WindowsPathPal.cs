using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PolyInstall.Pal;

internal static class WindowsPathPal
{
    [SupportedOSPlatform("windows")]
    public static void AddToPath(string directory, string scope)
    {
        var (root, subKey) = scope.Equals("machine", StringComparison.OrdinalIgnoreCase)
            ? (Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment")
            : (Registry.CurrentUser, "Environment");

        using var key = root.OpenSubKey(subKey, true)
                        ?? throw new InvalidOperationException($"Could not open registry key for PATH modification.");
        var existing = key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        var entries = existing.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var normalized = directory.TrimEnd('\\');
        if (entries.Any(e => e.TrimEnd('\\').Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            return;
        var newPath = existing.EndsWith(';') ? existing + normalized : existing + ";" + normalized;
        key.SetValue("Path", newPath, RegistryValueKind.ExpandString);
        NativeMethods.NotifyEnvironmentChange();
    }

    [SupportedOSPlatform("windows")]
    public static void RemoveFromPath(string directory, string scope)
    {
        var (root, subKey) = scope.Equals("machine", StringComparison.OrdinalIgnoreCase)
            ? (Registry.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment")
            : (Registry.CurrentUser, "Environment");

        using var key = root.OpenSubKey(subKey, true)
                        ?? throw new InvalidOperationException($"Could not open registry key for PATH modification.");
        var existing = key.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string ?? "";
        var entries = existing.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
        var normalized = directory.TrimEnd('\\');
        entries.RemoveAll(e => e.TrimEnd('\\').Equals(normalized, StringComparison.OrdinalIgnoreCase));
        key.SetValue("Path", string.Join(";", entries), RegistryValueKind.ExpandString);
        NativeMethods.NotifyEnvironmentChange();
    }
}
