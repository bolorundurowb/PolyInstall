namespace PolyInstall.Pal;

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
