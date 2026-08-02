using PolyInstall.Install;

namespace PolyInstall.Pal;

/// <summary>
/// Provides a platform abstraction layer (PAL) for core installation operations.
/// </summary>
public interface IPolyInstallPal : IInstallPathPal
{
    /// <summary>Gets the PAL for creating file shortcuts.</summary>
    IShortcutPal Shortcuts { get; }

    /// <summary>Gets the PAL for registry operations (Windows only).</summary>
    IRegistryPal? Registry { get; }

    /// <summary>Gets the PAL for creating desktop entries (Linux only).</summary>
    IDesktopEntryPal? DesktopEntries { get; }

    /// <summary>Gets the PAL for setting file permissions (POSIX only).</summary>
    IFilePermissionsPal? FilePermissions { get; }

    /// <summary>Gets the PAL for system PATH environment variable operations.</summary>
    IPathPal? Path { get; }

    /// <summary>Gets the PAL for file association operations.</summary>
    IFileAssociationPal? FileAssociations { get; }

    /// <summary>Gets the PAL for service management operations.</summary>
    IServiceManagerPal? Services { get; }

    /// <summary>Gets the PAL for process management operations.</summary>
    IProcessManagerPal Processes { get; }
}

/// <summary>
/// Provides operations for managing system PATH entries.
/// </summary>
public interface IPathPal
{
    /// <summary>
    /// Adds a directory to the system PATH.
    /// </summary>
    /// <param name="path">The directory path to add.</param>
    /// <param name="scope">The scope: "user" or "machine".</param>
    void AddToPath(string path, string scope);

    /// <summary>
    /// Removes a directory from the system PATH.
    /// </summary>
    /// <param name="path">The directory path to remove.</param>
    /// <param name="scope">The scope: "user" or "machine".</param>
    void RemoveFromPath(string path, string scope);

    /// <summary>Gets the list of paths added during the current session.</summary>
    IReadOnlyList<(string Path, string Scope)> AddedPaths { get; }
}

/// <summary>
/// Provides operations for creating file shortcuts.
/// </summary>
public interface IShortcutPal
{
    /// <summary>
    /// Creates a shortcut to a file.
    /// </summary>
    /// <param name="targetPath">The path to the target file.</param>
    /// <param name="shortcutPath">The path where the shortcut should be created.</param>
    /// <param name="description">An optional description for the shortcut.</param>
    /// <param name="iconPath">An optional path to an icon for the shortcut.</param>
    void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath);
}

/// <summary>
/// Provides operations for modifying the Windows Registry.
/// </summary>
public interface IRegistryPal
{
    /// <summary>
    /// Sets a value in the registry.
    /// </summary>
    /// <param name="keyPath">The path to the registry key.</param>
    /// <param name="valueName">The name of the value.</param>
    /// <param name="value">The value to set.</param>
    /// <param name="valueKind">The kind of the value (e.g., "String", "DWord").</param>
    void SetValue(string keyPath, string? valueName, string value, string valueKind);
}

/// <summary>
/// Provides operations for creating desktop entries (Linux only).
/// </summary>
public interface IDesktopEntryPal
{
    /// <summary>
    /// Creates a desktop entry file.
    /// </summary>
    /// <param name="fileName">The name of the desktop file (e.g., "myapp.desktop").</param>
    /// <param name="name">The display name of the application.</param>
    /// <param name="exec">The command to execute.</param>
    /// <param name="icon">The path or name of the icon.</param>
    /// <param name="comment">An optional comment for the desktop entry.</param>
    void CreateDesktopEntry(string fileName, string name, string exec, string? icon, string? comment);
}

/// <summary>
/// Provides operations for setting file system permissions.
/// </summary>
public interface IFilePermissionsPal
{
    /// <summary>
    /// Sets the file mode (permissions).
    /// </summary>
    /// <param name="path">The path to the file or directory.</param>
    /// <param name="mode">The octal mode (e.g., 0755).</param>
    void SetFileMode(string path, int mode);
}

/// <summary>
/// Provides operations for managing system services.
/// </summary>
public interface IServiceManagerPal
{
    /// <summary>
    /// Installs or updates a service registration.
    /// </summary>
    /// <param name="service">The service registration information.</param>
    void InstallOrUpdate(ServiceRegistrationInfo service);

    /// <summary>
    /// Removes a service registration.
    /// </summary>
    /// <param name="service">The registered service information.</param>
    void Remove(RegisteredServiceInfo service);

    /// <summary>Gets the list of services currently registered on the system.</summary>
    IReadOnlyList<RegisteredServiceInfo> RegisteredServices { get; }
}
