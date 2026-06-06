using System.ComponentModel;

namespace PolyInstall.Manifest;

public sealed class FilesEntry
{
    public string SourceDir { get; set; } = ".";
    public List<string> Include { get; set; } = ["**/*"];
    public List<string>? Exclude { get; set; }

    [Description("Optional list of feature ids this entry belongs to. When null or empty, the matched files are always installed (core). When set, the matched files are only installed if at least one referenced feature is selected.")]
    public List<string>? Features { get; set; }
}
