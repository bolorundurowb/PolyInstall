namespace PolyInstall.Install;

/// <summary>
/// Persisted to <c>.polyinstall/install-state.json</c> under the install directory.
/// </summary>
public sealed class InstallStateDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string ProductId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DisplayVersion { get; set; } = "";
    public string? Publisher { get; set; }
    public string InstallLocation { get; set; } = "";
    public string InstallScope { get; set; } = "user";
    public string RegistryUninstallKeyRelative { get; set; } = "";
    public List<string>? PayloadFiles { get; set; }
    public List<string>? AddedToPath { get; set; }
    public List<FileAssociationBackup>? FileAssociationBackups { get; set; }

    /// <summary>
    /// Feature ids that were selected by the user at install time. Used on update/uninstall
    /// to scope feature-gated tasks and file associations. Null/empty means either the
    /// manifest declared no features (legacy/full install) or an older installer that
    /// pre-dates feature support.
    /// </summary>
    public List<string>? SelectedFeatures { get; set; }
}

public sealed class FileAssociationBackup
{
    public string Extension { get; set; } = "";
    public string? OriginalProgId { get; set; }
    public string? OriginalMimeType { get; set; }
    public string? OriginalDefaultApp { get; set; }
    public string? OriginalInfoPlistContent { get; set; }
    public List<string>? BackupFilePaths { get; set; }
}
