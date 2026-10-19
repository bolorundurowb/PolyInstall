namespace PolyInstall.Pal;

/// <summary>
/// Platform elevation capability. Machine-scope installs may need to relaunch with higher
/// privileges; only Windows implements interactive UAC relaunch. Linux and macOS report
/// root via <see cref="IsElevated"/> and return <see cref="ElevationStatus.Unsupported"/>
/// from relaunch so the caller can instruct the operator to re-run under <c>sudo</c>.
/// </summary>
public interface IElevationPal
{
    bool IsElevated { get; }

    /// <summary>Start an elevated copy of <paramref name="exe"/> and return immediately.</summary>
    ElevationResult Relaunch(string exe, IReadOnlyList<string> args);

    /// <summary>Start an elevated copy, wait for it to exit, and return its exit code.</summary>
    ElevationResult RelaunchAndWait(string exe, IReadOnlyList<string> args);
}

public enum ElevationStatus
{
    Started = 0,
    Completed = 1,
    Denied = 2,
    Unsupported = 3,
    Failed = 4,
}

public readonly record struct ElevationResult(ElevationStatus Status, int ExitCode = 0)
{
    public static ElevationResult Started() => new(ElevationStatus.Started);
    public static ElevationResult Completed(int exitCode) => new(ElevationStatus.Completed, exitCode);
    public static ElevationResult Denied() => new(ElevationStatus.Denied);
    public static ElevationResult Unsupported() => new(ElevationStatus.Unsupported);
    public static ElevationResult Failed() => new(ElevationStatus.Failed);
}
