using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Declares an optional feature that the user can choose to install. Features gate
/// <see cref="FilesEntry"/> entries, <see cref="InstallTask"/> tasks, and
/// <see cref="FileAssociation"/> entries via their <c>features</c> reference list.
/// </summary>
public sealed class FeatureDefinition
{
    [Description("Unique identifier referenced by files[], tasks[], and file_associations[] entries. Lowercase snake_case is recommended.")]
    public string Id { get; set; } = "";

    [Description("Human-readable name shown in the installer's features step.")]
    public string Name { get; set; } = "";

    [Description("Short description shown alongside the feature checkbox in the installer.")]
    public string? Description { get; set; }

    [Description("Whether the feature is pre-checked on a fresh install. Defaults to true.")]
    public bool DefaultSelected { get; set; } = true;
}
