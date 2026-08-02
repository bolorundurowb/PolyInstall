using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Defines build-time configuration settings for creating an installer.
/// </summary>
public sealed class BuildConfiguration
{
    /// <summary>Gets or sets the directory where the built installer will be placed.</summary>
    [Description("Directory where the built installer will be placed.")]
    public string OutputDir { get; set; } = "dist";

    /// <summary>Gets or sets the base name for the output installer file.</summary>
    [Description("Base name for the output installer file.")]
    public string? OutputName { get; set; }

    /// <summary>Gets or sets the compression algorithm used for the payload (e.g., "brotli", "gzip").</summary>
    [Description("Compression algorithm used for the payload (e.g., 'brotli', 'gzip').")]
    public string Compression { get; set; } = "brotli";

    /// <summary>Gets or sets the list of target platforms to build for.</summary>
    [Description("List of target platforms to build for.")]
    public List<string> Targets { get; set; } = [];

    /// <summary>Gets or sets the specific target platform for the installer.</summary>
    [Description("Specific target platform for the installer.")]
    public string? InstallerTarget { get; set; }

    /// <summary>Gets or sets the path to the runtime stub executable.</summary>
    [Description("Path to the runtime stub executable.")]
    public string? StubPath { get; set; }

    /// <summary>Gets or sets the signing options for the installer.</summary>
    [Description("Signing options for the installer.")]
    public SigningBuildOptions? Signing { get; set; }

    /// <summary>Gets or sets Windows-specific build options.</summary>
    [Description("Windows-specific build options.")]
    public WindowsBuildOptions? Windows { get; set; }

    /// <summary>Gets or sets Linux-specific build options.</summary>
    [Description("Linux-specific build options.")]
    public LinuxBuildOptions? Linux { get; set; }

    /// <summary>Gets or sets macOS-specific build options.</summary>
    [Description("macOS-specific build options.")]
    public MacOsBuildOptions? Macos { get; set; }
}
