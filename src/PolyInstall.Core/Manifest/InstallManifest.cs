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
    public List<ServiceDefinition>? Services { get; set; }
    public TasksConfiguration? Tasks { get; set; }
    public List<FeatureDefinition>? Features { get; set; }

    /// <summary>
    /// Build-time index mapping payload files to features. Produced by the build pipeline
    /// and consumed by the runtime stub. Omitted from output when null.
    /// </summary>
    public PayloadFeatureIndex? FeatureIndex { get; set; }

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
