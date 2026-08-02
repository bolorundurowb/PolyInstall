namespace PolyInstall.Install;

/// <summary>
/// Represents the persisted installation state, stored as <c>.polyinstall/install-state.json</c>
/// within the installation directory.
/// </summary>
public sealed class InstallStateDocument
{
    /// <summary>Gets or sets the version of the state document schema.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Gets or sets the unique product identifier.</summary>
    public string ProductId { get; set; } = "";

    /// <summary>Gets or sets the display name of the application.</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>Gets or sets the version of the installed application.</summary>
    public string DisplayVersion { get; set; } = "";

    /// <summary>Gets or sets the name of the application publisher.</summary>
    public string? Publisher { get; set; }

    /// <summary>Gets or sets the location where the application is installed.</summary>
    public string InstallLocation { get; set; } = "";

    /// <summary>Gets or sets the installation scope (e.g., "user" or "machine").</summary>
    public string InstallScope { get; set; } = "user";

    /// <summary>Gets or sets the relative registry key path for uninstallation (Windows only).</summary>
    public string RegistryUninstallKeyRelative { get; set; } = "";

    /// <summary>Gets or sets the list of payload files installed.</summary>
    public List<string>? PayloadFiles { get; set; }

    /// <summary>Gets or sets the list of paths added to the system PATH environment variable.</summary>
    public List<string>? AddedToPath { get; set; }

    /// <summary>Gets or sets the backups of original file associations for restoration during uninstallation.</summary>
    public List<FileAssociationBackup>? FileAssociationBackups { get; set; }

    /// <summary>Gets or sets the list of services registered during installation.</summary>
    public List<RegisteredServiceInfo>? RegisteredServices { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of features selected during installation.
    /// Used during updates and uninstallation to scope feature-gated tasks.
    /// </summary>
    public List<string>? SelectedFeatures { get; set; }
}

/// <summary>
/// Contains backup information for a file association that was overridden during installation.
/// </summary>
public sealed class FileAssociationBackup
{
    /// <summary>Gets or sets the file extension.</summary>
    public string Extension { get; set; } = "";

    /// <summary>Gets or sets the original ProgID associated with the extension (Windows only).</summary>
    public string? OriginalProgId { get; set; }

    /// <summary>Gets or sets the original MIME type associated with the extension (Linux only).</summary>
    public string? OriginalMimeType { get; set; }

    /// <summary>Gets or sets the original default application (macOS only).</summary>
    public string? OriginalDefaultApp { get; set; }

    /// <summary>Gets or sets the original Info.plist content (macOS only).</summary>
    public string? OriginalInfoPlistContent { get; set; }

    /// <summary>Gets or sets the list of paths to backup files created during the association process.</summary>
    public List<string>? BackupFilePaths { get; set; }
}
