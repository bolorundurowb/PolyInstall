using PolyInstall.Install;

namespace PolyInstall.Pal;

public interface IPolyInstallPal : IInstallPathPal
{
    IShortcutPal Shortcuts { get; }
    IRegistryPal? Registry { get; }
    IDesktopEntryPal? DesktopEntries { get; }
    IFilePermissionsPal? FilePermissions { get; }
    IPathPal? Path { get; }
    IFileAssociationPal? FileAssociations { get; }
    IServiceManagerPal? Services { get; }
    IProcessManagerPal Processes { get; }
}

public interface IPathPal
{
    void AddToPath(string path, string scope);
    void RemoveFromPath(string path, string scope);
    IReadOnlyList<(string Path, string Scope)> AddedPaths { get; }
}

public interface IShortcutPal
{
    void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath);
}

public interface IRegistryPal
{
    void SetValue(string keyPath, string? valueName, string value, string valueKind);
}

public interface IDesktopEntryPal
{
    void CreateDesktopEntry(string fileName, string name, string exec, string? icon, string? comment);
}

public interface IFilePermissionsPal
{
    void SetFileMode(string path, int mode);
}

public interface IServiceManagerPal
{
    void InstallOrUpdate(ServiceRegistrationInfo service);
    void Remove(RegisteredServiceInfo service);
    IReadOnlyList<RegisteredServiceInfo> RegisteredServices { get; }
}
