namespace PolyInstall.Install;

/// <summary>
/// Contains information about an existing installation of the application.
/// </summary>
public sealed class ExistingInstallInfo
{
    /// <summary>Gets the unique product identifier.</summary>
    public string ProductId { get; init; } = "";

    /// <summary>Gets the display name of the application.</summary>
    public string DisplayName { get; init; } = "";

    /// <summary>Gets the version of the installed application.</summary>
    public string DisplayVersion { get; init; } = "";

    /// <summary>Gets the name of the application publisher.</summary>
    public string? Publisher { get; init; }

    /// <summary>Gets the location where the application is installed.</summary>
    public string InstallLocation { get; init; } = "";

    /// <summary>Gets the installation scope (e.g., "user" or "machine").</summary>
    public string InstallScope { get; init; } = "user";

    /// <summary>Gets the source from which the installation information was retrieved.</summary>
    public ExistingInstallSource Source { get; init; } = ExistingInstallSource.InstallState;

    /// <summary>Gets the detailed installation state document, if available.</summary>
    public InstallStateDocument? State { get; init; }

    /// <summary>Gets a value indicating whether the existing installation has a payload file inventory.</summary>
    public bool HasPayloadFileInventory => State?.PayloadFiles is { Count: > 0 };
}

/// <summary>
/// Specifies the source of existing installation information.
/// </summary>
public enum ExistingInstallSource
{
    /// <summary>Information retrieved from the <c>.install_state.json</c> file.</summary>
    InstallState,
    /// <summary>Information retrieved from the Windows Add/Remove Programs (ARP) registry.</summary>
    WindowsArp,
}
