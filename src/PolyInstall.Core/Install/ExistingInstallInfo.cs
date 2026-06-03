namespace PolyInstall.Core.Install;

public sealed class ExistingInstallInfo
{
    public string ProductId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string DisplayVersion { get; init; } = "";
    public string? Publisher { get; init; }
    public string InstallLocation { get; init; } = "";
    public string InstallScope { get; init; } = "user";
    public ExistingInstallSource Source { get; init; } = ExistingInstallSource.InstallState;
    public InstallStateDocument? State { get; init; }
    public bool HasPayloadFileInventory => State?.PayloadFiles is { Count: > 0 };
}

public enum ExistingInstallSource
{
    InstallState,
    WindowsArp,
}
