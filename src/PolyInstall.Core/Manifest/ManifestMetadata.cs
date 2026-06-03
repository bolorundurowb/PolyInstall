namespace PolyInstall.Manifest;

public sealed class ManifestMetadata
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string? Id { get; set; }
    public string? Publisher { get; set; }
}
