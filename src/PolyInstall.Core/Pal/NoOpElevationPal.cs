namespace PolyInstall.Pal;

/// <summary>Test double that never elevates and reports a fixed elevation state.</summary>
public sealed class NoOpElevationPal : IElevationPal
{
    public NoOpElevationPal(bool isElevated = true) => IsElevated = isElevated;

    public bool IsElevated { get; }

    public ElevationResult Relaunch(string exe, IReadOnlyList<string> args) =>
        ElevationResult.Unsupported();

    public ElevationResult RelaunchAndWait(string exe, IReadOnlyList<string> args) =>
        ElevationResult.Unsupported();
}
