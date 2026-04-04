namespace PolyInstall.Core.Manifest;

public sealed class BuildConfiguration
{
    public string OutputDir { get; set; } = "dist";
    public string Compression { get; set; } = "brotli";
    public List<string> Targets { get; set; } = [];
    public string? StubPath { get; set; }
}
