using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Contains descriptive metadata for an application.
/// </summary>
public sealed class ManifestMetadata
{
    /// <summary>Gets or sets the display name of the application.</summary>
    [Description("Display name of the application.")]
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the version of the application.</summary>
    [Description("Version of the application.")]
    public string Version { get; set; } = "";

    /// <summary>Gets or sets a unique identifier for the application.</summary>
    [Description("Unique identifier for the application.")]
    public string? Id { get; set; }

    /// <summary>Gets or sets the name of the application publisher.</summary>
    [Description("Name of the application publisher.")]
    public string? Publisher { get; set; }
}
