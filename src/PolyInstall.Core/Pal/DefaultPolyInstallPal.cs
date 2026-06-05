using PolyInstall.Hosting;

namespace PolyInstall.Pal;

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
        Path = new PathPal();
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
