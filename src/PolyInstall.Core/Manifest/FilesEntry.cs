namespace PolyInstall.Core.Manifest;

public sealed class FilesEntry
{
    public string SourceDir { get; set; } = ".";
    public List<string> Include { get; set; } = ["**/*"];
    public List<string>? Exclude { get; set; }
}
