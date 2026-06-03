namespace PolyInstall.Core.Install;

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
}
