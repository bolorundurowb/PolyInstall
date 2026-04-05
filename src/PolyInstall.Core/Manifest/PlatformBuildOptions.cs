namespace PolyInstall.Core.Manifest;

public sealed class WindowsBuildOptions
{
    /// <summary>Per-user (HKCU) or per-machine (HKLM) ARP registration.</summary>
    public string InstallScope { get; set; } = "user";

    /// <summary>Register Add/Remove Programs after install (Windows only).</summary>
    public bool RegisterArp { get; set; } = true;
}

public sealed class LinuxBuildOptions
{
    /// <summary><c>none</c> (raw ELF+bundle) or <c>appimage</c>.</summary>
    public string Package { get; set; } = "none";
}

public sealed class MacOsBuildOptions
{
    /// <summary><c>none</c> (raw Mach-O+bundle) or <c>dmg</c>.</summary>
    public string Package { get; set; } = "none";
}
