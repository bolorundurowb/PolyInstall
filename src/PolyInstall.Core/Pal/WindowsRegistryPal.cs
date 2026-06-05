using System.Runtime.Versioning;
using Microsoft.Win32;

namespace PolyInstall.Pal;

internal sealed class WindowsRegistryPal : IRegistryPal
{
    [SupportedOSPlatform("windows")]
    public void SetValue(string keyPath, string? valueName, string value, string valueKind)
    {
        var parts = keyPath.Split('\\', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new ArgumentException("Expected key path like HKCU\\Software\\...", nameof(keyPath));
        var root = parts[0].ToUpperInvariant() switch
        {
            "HKCU" or "HKEY_CURRENT_USER" => Registry.CurrentUser,
            "HKLM" or "HKEY_LOCAL_MACHINE" => Registry.LocalMachine,
            _ => throw new NotSupportedException($"Registry root not supported: {parts[0]}"),
        };
        using var key = root.CreateSubKey(parts[1], true)
                        ?? throw new InvalidOperationException($"Could not open or create {keyPath}.");
        var kind = valueKind.ToLowerInvariant() switch
        {
            "string" or "reg_sz" => RegistryValueKind.String,
            "expand_string" or "reg_expand_sz" => RegistryValueKind.ExpandString,
            "dword" or "reg_dword" => RegistryValueKind.DWord,
            _ => RegistryValueKind.String,
        };
        key.SetValue(valueName ?? "", value, kind);
    }
}
