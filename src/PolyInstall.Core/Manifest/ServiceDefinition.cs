using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Defines a service to be registered and managed by the target operating system's service manager.
/// </summary>
public sealed class ServiceDefinition
{
    /// <summary>
    /// Gets or sets the optional OS predicate that must be true for this service to be installed.
    /// Supported values: <c>os.isWindows</c>, <c>os.isLinux</c>, <c>os.isMacOS</c>, or <c>os.isOSX</c>.
    /// </summary>
    [Description("Optional OS predicate that must be true for this service to be installed. Supported values: os.isWindows, os.isLinux, os.isMacOS, or os.isOSX.")]
    public string? Require { get; set; }

    /// <summary>
    /// Gets or sets the service name.
    /// On macOS, this is used as the launchd Label; reverse-DNS names are recommended.
    /// </summary>
    [Description("Service name. On macOS this is used as the launchd Label; reverse-DNS names are recommended.")]
    public string Name { get; set; } = "";

    /// <summary>Gets or sets the optional display name for platforms that distinguish it from the service name.</summary>
    [Description("Optional display name for platforms that distinguish it from the service name.")]
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets the optional service description.</summary>
    [Description("Optional service description.")]
    public string? Description { get; set; }

    /// <summary>Gets or sets the service scope: <c>system</c> or <c>user</c>. Windows supports system only. Defaults to <c>system</c>.</summary>
    [Description("Service scope: system or user. Windows supports system only. Defaults to system.")]
    public string Scope { get; set; } = "system";

    /// <summary>Gets or sets a value indicating whether the service should be enabled for startup. Defaults to true.</summary>
    [Description("Whether the service should be enabled for startup. Defaults to true.")]
    public bool Enabled { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the service should be started immediately after installation. Defaults to false.</summary>
    [Description("Whether the service should be started immediately after installation. Defaults to false.")]
    public bool Start { get; set; }

    /// <summary>Gets or sets the path to the service executable. Supports PolyInstall path placeholders.</summary>
    [Description("Path to the service executable. Supports PolyInstall path placeholders.")]
    public string Executable { get; set; } = "";

    /// <summary>Gets or sets the optional command-line arguments passed to the service executable.</summary>
    [Description("Optional command-line arguments passed to the service executable.")]
    public List<string>? Arguments { get; set; }

    /// <summary>Gets or sets the optional working directory. Supports PolyInstall path placeholders.</summary>
    [Description("Optional working directory. Supports PolyInstall path placeholders.")]
    public string? WorkingDirectory { get; set; }

    /// <summary>Gets or sets the optional restart policy. Supported values are platform-specific.</summary>
    [Description("Optional restart policy. Supported values are platform-specific.")]
    public string? Restart { get; set; }

    /// <summary>Gets or sets the optional environment variables for the service.</summary>
    [Description("Optional environment variables for the service.")]
    public Dictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// Gets or sets the optional list of feature identifiers that gate this service.
    /// When null or empty, the service is always installed.
    /// </summary>
    [Description("Optional list of feature identifiers that gate this service. When null or empty, the service is always installed.")]
    public List<string>? Features { get; set; }
}
