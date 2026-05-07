using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using PolyInstall.Core.Hosting;
using PolyInstall.Core.Pal;

namespace PolyInstall.Runtime.Pal;

public sealed class DefaultPolyInstallPal : IPolyInstallPal
{
    public DefaultPolyInstallPal()
    {
        UserHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        ProgramFiles = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            : OperatingSystem.IsMacOS()
                ? "/Applications"
                : "/usr/local";
        Shortcuts = new DefaultShortcutPal();
        Registry = OperatingSystem.IsWindows() ? new WindowsRegistryPal() : null;
        DesktopEntries = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? new UnixDesktopEntryPal() : null;
        FilePermissions = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? new UnixFilePermissionsPal() : null;
    }

    public string AppDir => InstallBootstrap.InstallDirectory ?? InstallBootstrap.ExtractRoot;
    public string ProgramFiles { get; }
    public string UserHome { get; }
    public string Desktop { get; }
    public IShortcutPal Shortcuts { get; }
    public IRegistryPal? Registry { get; }
    public IDesktopEntryPal? DesktopEntries { get; }
    public IFilePermissionsPal? FilePermissions { get; }
}

internal sealed class DefaultShortcutPal : IShortcutPal
{
    public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
        if (OperatingSystem.IsWindows())
            WindowsShortcut.Create(targetPath, shortcutPath, description, iconPath);
        else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            UnixSymlinkShortcut.Create(targetPath, shortcutPath);
        else
            throw new PlatformNotSupportedException("Shortcuts are not supported on this OS.");
    }
}

internal static class WindowsShortcut
{
    [SupportedOSPlatform("windows")]
    public static void Create(string targetPath, string shortcutPath, string? description, string? iconPath)
    {
        var script =
            "$w = New-Object -ComObject WScript.Shell; " +
            "$s = $w.CreateShortcut(" + PsQ(shortcutPath) + "); " +
            "$s.TargetPath = " + PsQ(targetPath) + "; " +
            "$s.Description = " + PsQ(description ?? "") + "; " +
            "$s.IconLocation = " + PsQ(iconPath ?? "") + "; " +
            "$s.Save()";
        var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {enc}",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        p?.WaitForExit();
        if (p?.ExitCode != 0)
            throw new InvalidOperationException($"Shortcut creation failed (exit {p?.ExitCode}).");
    }

    private static string PsQ(string s) => "'" + s.Replace("'", "''", StringComparison.Ordinal) + "'";
}

internal static class UnixSymlinkShortcut
{
    public static void Create(string targetPath, string shortcutPath)
    {
        if (File.Exists(shortcutPath) || Directory.Exists(shortcutPath))
            File.Delete(shortcutPath);
        try
        {
            File.CreateSymbolicLink(shortcutPath, targetPath);
        }
        catch (PlatformNotSupportedException)
        {
            File.WriteAllText(shortcutPath, "#!/bin/sh\nexec \"" + targetPath.Replace("\"", "\\\"") + "\" \"$@\"\n");
            Chmod(shortcutPath, 0b111_101_101);
        }
    }

    private static void Chmod(string path, int mode)
    {
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"{Convert.ToString(mode, 8)} {path}",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        p?.WaitForExit();
    }
}

internal sealed class WindowsRegistryPal : IRegistryPal
{
    [SupportedOSPlatform("windows")]
    public void SetValue(string keyPath, string? valueName, string value, string valueKind)
    {
        // keyPath like HKCU\Software\Vendor\App
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

internal sealed class UnixDesktopEntryPal : IDesktopEntryPal
{
    public void CreateDesktopEntry(string fileName, string name, string exec, string? icon, string? comment)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".desktop");
        var lines = new List<string>
        {
            "[Desktop Entry]",
            "Type=Application",
            $"Name={name}",
            $"Exec={exec}",
            "Terminal=false",
        };
        if (!string.IsNullOrEmpty(icon))
            lines.Add($"Icon={icon}");
        if (!string.IsNullOrEmpty(comment))
            lines.Add($"Comment={comment}");
        File.WriteAllText(path, string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }
}

internal sealed class UnixFilePermissionsPal : IFilePermissionsPal
{
    public void SetUnixFileMode(string path, int mode)
    {
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = "chmod",
            Arguments = $"{Convert.ToString(mode, 8)} {path}",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        p?.WaitForExit();
        if (p?.ExitCode != 0)
            throw new InvalidOperationException($"chmod failed for {path}.");
    }
}
