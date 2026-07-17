using PolyInstall.Hosting;

namespace PolyInstall.Pal;

public sealed class DefaultPolyInstallPal : IPolyInstallPal
{
    public DefaultPolyInstallPal()
    {
        UserHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(LocalAppData))
            LocalAppData = UserHome;
        Desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        ProgramFiles = OperatingSystem.IsWindows()
            ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
            : OperatingSystem.IsMacOS()
                ? "/Applications"
                : "/usr/local";
        Shortcuts = new DefaultShortcutPal();
        Registry = OperatingSystem.IsWindows() ? new WindowsRegistryPal() : null;
        DesktopEntries = OperatingSystem.IsLinux() ? new LinuxDesktopEntryPal() : null;
        FilePermissions = OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() ? new PosixFilePermissionsPal() : null;
        FileAssociations = OperatingSystem.IsWindows()
            ? new WindowsFileAssociationPal()
            : OperatingSystem.IsLinux()
                ? new LinuxFileAssociationPal()
                : OperatingSystem.IsMacOS()
                    ? new MacOsFileAssociationPal()
                    : null;
        Services = OperatingSystem.IsWindows()
            ? new WindowsServiceManagerPal()
            : OperatingSystem.IsLinux()
                ? new LinuxSystemdServiceManagerPal()
                : OperatingSystem.IsMacOS()
                    ? new MacOsLaunchdServiceManagerPal()
                    : null;
        Path = new PathPal();
    }

    public string AppDir => InstallBootstrap.InstallDirectory ?? InstallBootstrap.ExtractRoot;
    public string ProgramFiles { get; }
    public string LocalAppData { get; }
    public string UserHome { get; }
    public string Desktop { get; }
    public IShortcutPal Shortcuts { get; }
    public IRegistryPal? Registry { get; }
    public IDesktopEntryPal? DesktopEntries { get; }
    public IFilePermissionsPal? FilePermissions { get; }
    public IFileAssociationPal? FileAssociations { get; }
    public IPathPal? Path { get; }
    public IServiceManagerPal? Services { get; }
}
