using System.ComponentModel;

namespace PolyInstall.Manifest;

public sealed class ServiceDefinition
{
    [Description(
        "Optional OS predicate that must be true for this service to be installed. " +
        "Use os.isWindows, os.isLinux, or os.isMacOS/os.isOSX to target the platform service manager.")]
    public string? Require { get; set; }

    [Description("Service name. On macOS this is used as the launchd Label; reverse-DNS names are recommended.")]
    public string Name { get; set; } = "";

    [Description("Optional display name for platforms that distinguish it from the service name.")]
    public string? DisplayName { get; set; }

    [Description("Optional service description.")]
    public string? Description { get; set; }

    [Description("Service scope: system or user. Windows supports system only. Defaults to system.")]
    public string Scope { get; set; } = "system";

    [Description("Whether the service should be enabled for startup. Defaults to true.")]
    public bool Enabled { get; set; } = true;

    [Description("Whether the service should be started immediately after installation. Defaults to false.")]
    public bool Start { get; set; }

    [Description("Path to the service executable. String values support PolyInstall path placeholders.")]
    public string Executable { get; set; } = "";

    [Description("Optional command-line arguments passed to the service executable.")]
    public List<string>? Arguments { get; set; }

    [Description("Optional working directory. String values support PolyInstall path placeholders.")]
    public string? WorkingDirectory { get; set; }

    [Description("Optional restart policy. Supported values are platform-specific.")]
    public string? Restart { get; set; }

    [Description("Optional environment variables for service managers that support them.")]
    public Dictionary<string, string>? Environment { get; set; }

    [Description("Optional feature ids that gate this service. When null or empty, the service always applies.")]
    public List<string>? Features { get; set; }
}
