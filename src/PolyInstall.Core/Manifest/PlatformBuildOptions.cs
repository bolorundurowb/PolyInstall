using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Defines Windows-specific build options.
/// </summary>
public sealed class WindowsBuildOptions
{
    /// <summary>Gets or sets the installation scope: <c>user</c> (HKCU) or <c>machine</c> (HKLM).</summary>
    [Description("Installation scope: user (HKCU) or machine (HKLM).")]
    public string InstallScope { get; set; } = "user";

    /// <summary>Gets or sets a value indicating whether to register the application in Add/Remove Programs (Windows only).</summary>
    [Description("Whether to register the application in Add/Remove Programs (Windows only).")]
    public bool RegisterArp { get; set; } = true;
}

/// <summary>
/// Defines Linux-specific build options.
/// </summary>
public sealed class LinuxBuildOptions
{
    /// <summary>Gets or sets the package type: <c>none</c> (raw ELF and bundle) or <c>appimage</c>.</summary>
    [Description("Package type: none (raw ELF and bundle) or appimage.")]
    public string Package { get; set; } = "none";
}

/// <summary>
/// Defines macOS-specific build options.
/// </summary>
public sealed class MacOsBuildOptions
{
    /// <summary>Gets or sets the package type: <c>none</c> (raw Mach-O and bundle) or <c>dmg</c>.</summary>
    [Description("Package type: none (raw Mach-O and bundle) or dmg.")]
    public string Package { get; set; } = "none";
}
