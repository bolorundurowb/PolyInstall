using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using PolyInstall.Hosting;

namespace PolyInstall.Pal;

public sealed class DefaultPolyInstallPal : IPolyInstallPal
{
    private readonly PathPal _pathPal;

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
        _pathPal = new PathPal();
        Path = _pathPal;
    }

    public string AppDir => InstallBootstrap.InstallDirectory ?? InstallBootstrap.ExtractRoot;
    public string ProgramFiles { get; }
    public string UserHome { get; }
    public string Desktop { get; }
    public IShortcutPal Shortcuts { get; }
    public IRegistryPal? Registry { get; }
    public IDesktopEntryPal? DesktopEntries { get; }
    public IFilePermissionsPal? FilePermissions { get; }
    public IPathPal? Path { get; }
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
        var script = new StringBuilder();
        script.Append("$w = New-Object -ComObject WScript.Shell; ");
        script.Append("$s = $w.CreateShortcut(" + PsQ(shortcutPath) + "); ");
        script.Append("$s.TargetPath = " + PsQ(targetPath) + "; ");
        if (!string.IsNullOrEmpty(description))
            script.Append("$s.Description = " + PsQ(description) + "; ");
        if (!string.IsNullOrEmpty(iconPath))
            script.Append("$s.IconLocation = " + PsQ(iconPath) + "; ");
        script.Append("$s.Save()");

        var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script.ToString()));
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {enc}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi);
        p?.WaitForExit();
        if (p?.ExitCode != 0)
        {
            var stdout = p?.StandardOutput.ReadToEnd() ?? "";
            var stderr = p?.StandardError.ReadToEnd() ?? "";
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            if (!string.IsNullOrWhiteSpace(detail))
                throw new InvalidOperationException($"Shortcut creation failed (exit {p?.ExitCode}): {detail.Trim()}");
            throw new InvalidOperationException($"Shortcut creation failed (exit {p?.ExitCode}).");
        }
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
            File.WriteAllText(shortcutPath, "#!/bin/sh\nexec \"" + targetPath.Replace("\"", "\\\"", StringComparison.Ordinal) + "\" \"$@\"\n");
            Chmod(shortcutPath, 0b111_101_101);
        }
    }

    private static void Chmod(string path, int mode)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "chmod",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(Convert.ToString(mode, 8));
        psi.ArgumentList.Add(path);
        using var p = Process.Start(psi);
        p?.WaitForExit();
    }
}

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
        var psi = new ProcessStartInfo
        {
            FileName = "chmod",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(Convert.ToString(mode, 8));
        psi.ArgumentList.Add(path);
        using var p = Process.Start(psi);
        p?.WaitForExit();
        if (p?.ExitCode != 0)
            throw new InvalidOperationException($"chmod failed for {path}.");
    }
}

internal sealed class PathPal : IPathPal
{
    private readonly List<(string Path, string Scope)> _addedPaths = [];

    public void AddToPath(string path, string scope)
    {
        if (OperatingSystem.IsWindows())
            WindowsPathPal.AddToPath(path, scope);
        else
            UnixPathPal.AddToPath(path, scope);

        _addedPaths.Add((path, scope));
    }

    public void RemoveFromPath(string path, string scope)
    {
        if (OperatingSystem.IsWindows())
            WindowsPathPal.RemoveFromPath(path, scope);
        else
            UnixPathPal.RemoveFromPath(path, scope);
    }

    public IReadOnlyList<(string Path, string Scope)> AddedPaths => _addedPaths;
}

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

internal static class UnixPathPal
{
    public static void AddToPath(string directory, string scope)
    {
        if (scope.Equals("machine", StringComparison.OrdinalIgnoreCase))
        {
            if (OperatingSystem.IsMacOS())
            {
                var profilePath = "/etc/paths.d";
                if (!Directory.Exists(profilePath))
                    Directory.CreateDirectory(profilePath);
                var fileName = SanitizeFileName(directory);
                File.WriteAllText(Path.Combine(profilePath, fileName), directory + "\n");
            }
            else
            {
                var profilePath = "/etc/profile.d";
                if (!Directory.Exists(profilePath))
                    Directory.CreateDirectory(profilePath);
                var fileName = SanitizeFileName(directory) + ".sh";
                File.WriteAllText(Path.Combine(profilePath, fileName),
                    $"export PATH=\"$PATH:{directory}\"\n");
            }
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var profileFile = FindShellProfile(home);
            var entry = $"export PATH=\"$PATH:{directory}\"";
            if (File.Exists(profileFile) && File.ReadAllText(profileFile).Contains(entry, StringComparison.Ordinal))
                return;
            File.AppendAllText(profileFile, $"\n{entry}\n");
        }
    }

    public static void RemoveFromPath(string directory, string scope)
    {
        if (scope.Equals("machine", StringComparison.OrdinalIgnoreCase))
        {
            if (OperatingSystem.IsMacOS())
            {
                var pathsDir = "/etc/paths.d";
                var fileName = SanitizeFileName(directory);
                var filePath = Path.Combine(pathsDir, fileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            else
            {
                var profileDir = "/etc/profile.d";
                var fileName = SanitizeFileName(directory) + ".sh";
                var filePath = Path.Combine(profileDir, fileName);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var profileFile = FindShellProfile(home);
            if (!File.Exists(profileFile))
                return;
            var entry = $"export PATH=\"$PATH:{directory}\"";
            var lines = File.ReadAllLines(profileFile)
                .Where(l => !l.Trim().Equals(entry, StringComparison.Ordinal))
                .ToArray();
            File.WriteAllLines(profileFile, lines);
        }
    }

    private static string FindShellProfile(string home)
    {
        var bashrc = Path.Combine(home, ".bashrc");
        if (File.Exists(bashrc))
            return bashrc;
        var zshrc = Path.Combine(home, ".zshrc");
        if (File.Exists(zshrc))
            return zshrc;
        var profile = Path.Combine(home, ".profile");
        if (File.Exists(profile))
            return profile;
        return bashrc;
    }

    private static string SanitizeFileName(string path)
    {
        var name = path.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim('_');
    }
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [System.Runtime.InteropServices.DefaultDllImportSearchPaths(System.Runtime.InteropServices.DllImportSearchPath.System32)]
    private static extern bool SendMessageNotifyAll(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_SETTINGCHANGE = 0x001A;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    public static void NotifyEnvironmentChange()
    {
        if (!OperatingSystem.IsWindows())
            return;

        SendMessageNotifyAll(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, IntPtr.Zero);
    }
}
