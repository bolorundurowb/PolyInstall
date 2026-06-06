using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyInstall.Manifest;

public sealed class InstallManifest
{
    public ManifestMetadata Metadata { get; set; } = new();
    public BuildConfiguration Build { get; set; } = new();
    public UiConfiguration Ui { get; set; } = new();
    public List<FilesEntry> Files { get; set; } = [];
    public List<FileAssociation>? FileAssociations { get; set; }
    public TasksConfiguration? Tasks { get; set; }

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
