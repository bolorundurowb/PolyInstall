using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyInstall.Manifest;

/// <summary>
/// Represents the root configuration for an application installation.
/// This manifest defines the application metadata, files, services, and installation steps.
/// </summary>
public sealed class InstallManifest
{
    /// <summary>Gets or sets the core metadata for the application, such as name and version.</summary>
    [Description("Core metadata for the application, such as name and version.")]
    public ManifestMetadata Metadata { get; set; } = new();

    /// <summary>Gets or sets the build-time configuration settings.</summary>
    [Description("Build-time configuration settings.")]
    public BuildConfiguration Build { get; set; } = new();

    /// <summary>Gets or sets the user interface configuration for the installer.</summary>
    [Description("User interface configuration for the installer.")]
    public UiConfiguration Ui { get; set; } = new();

    /// <summary>Gets or sets the collection of file entries to be installed.</summary>
    [Description("Collection of file entries to be installed.")]
    public List<FilesEntry> Files { get; set; } = [];

    /// <summary>Gets or sets the optional list of file associations to be registered on the target system.</summary>
    [Description("Optional list of file associations to be registered on the target system.")]
    public List<FileAssociation>? FileAssociations { get; set; }

    /// <summary>Gets or sets the optional list of services to be registered and managed.</summary>
    [Description("Optional list of services to be registered and managed.")]
    public List<ServiceDefinition>? Services { get; set; }

    /// <summary>Gets or sets the optional configuration for pre-install and post-install tasks.</summary>
    [Description("Optional configuration for pre-install and post-install tasks.")]
    public TasksConfiguration? Tasks { get; set; }

    /// <summary>Gets or sets the optional list of features that can be selectively installed.</summary>
    [Description("Optional list of features that can be selectively installed.")]
    public List<FeatureDefinition>? Features { get; set; }

    /// <summary>
    /// Gets or sets the build-time index mapping payload files to features.
    /// This is produced by the build pipeline and consumed by the runtime stub.
    /// Omitted from output when null.
    /// </summary>
    public PayloadFeatureIndex? FeatureIndex { get; set; }

    /// <summary>Gets the default JSON serialization options used for the manifest.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
