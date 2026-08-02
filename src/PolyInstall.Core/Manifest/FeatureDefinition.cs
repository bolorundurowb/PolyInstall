using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Declares an optional feature that the user can choose to install.
/// Features gate <see cref="FilesEntry"/> entries, installation tasks, and
/// <see cref="FileAssociation"/> entries via their features reference list.
/// </summary>
public sealed class FeatureDefinition
{
    /// <summary>
    /// Gets or sets the unique identifier for the feature.
    /// This ID is referenced by files, tasks, and file associations.
    /// Lowercase snake_case is recommended.
    /// </summary>
    [Description("Unique identifier for the feature. This ID is referenced by files, tasks, and file associations. Lowercase snake_case is recommended.")]
    public string Id { get; set; } = "";

    /// <summary>Gets or sets the human-readable name shown in the installer's features step.</summary>
    [Description("Human-readable name shown in the installer's features step.")]
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the short description shown alongside the feature checkbox in the installer.</summary>
    [Description("Short description shown alongside the feature checkbox in the installer.")]
    public string? Description { get; set; }

    /// <summary>Gets or sets a value indicating whether the feature is pre-checked on a fresh install. Defaults to true.</summary>
    [Description("Whether the feature is pre-checked on a fresh install. Defaults to true.")]
    public bool DefaultSelected { get; set; } = true;
}
