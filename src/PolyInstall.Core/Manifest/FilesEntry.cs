using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Defines a set of files to be included in the installation.
/// </summary>
public sealed class FilesEntry
{
    /// <summary>Gets or sets the source directory for the files, relative to the project root.</summary>
    [Description("Source directory for the files, relative to the project root.")]
    public string SourceDir { get; set; } = ".";

    /// <summary>Gets or sets the list of glob patterns to include.</summary>
    [Description("List of glob patterns to include.")]
    public List<string> Include { get; set; } = ["**/*"];

    /// <summary>Gets or sets the list of glob patterns to exclude.</summary>
    [Description("List of glob patterns to exclude.")]
    public List<string>? Exclude { get; set; }

    /// <summary>
    /// Gets or sets the optional list of feature identifiers this entry belongs to.
    /// When null or empty, the matched files are always installed (core).
    /// When set, the matched files are only installed if at least one referenced feature is selected.
    /// </summary>
    [Description("Optional list of feature identifiers this entry belongs to. When null or empty, the matched files are always installed (core). When set, the matched files are only installed if at least one referenced feature is selected.")]
    public List<string>? Features { get; set; }
}
