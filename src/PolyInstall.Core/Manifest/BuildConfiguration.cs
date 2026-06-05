namespace PolyInstall.Manifest;

public sealed class BuildConfiguration
{
    public string OutputDir { get; set; } = "dist";
    public string? OutputName { get; set; }
    public string Compression { get; set; } = "brotli";
    public List<string> Targets { get; set; } = [];
    public string? InstallerTarget { get; set; }
    public string? StubPath { get; set; }
    public SigningBuildOptions? Signing { get; set; }
    public WindowsBuildOptions? Windows { get; set; }
    public LinuxBuildOptions? Linux { get; set; }
    public MacOsBuildOptions? Macos { get; set; }
}
