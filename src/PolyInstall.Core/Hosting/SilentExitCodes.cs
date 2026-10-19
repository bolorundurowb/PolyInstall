namespace PolyInstall.Hosting;

/// <summary>Exit codes returned by silent / unattended installer runs.</summary>
public static class SilentExitCodes
{
    public const int Success = 0;
    public const int UnexpectedError = 1;
    public const int InvalidArguments = 2;
    public const int ElevationRequired = 3;
    public const int ProcessesRunning = 4;
    public const int Cancelled = 5;
    public const int EulaNotAccepted = 6;
    public const int ExistingInstallBlocks = 7;
}
