namespace PolyInstall.Pal;

/// <summary>
/// Elevation for Linux and macOS. Detects root via <see cref="PosixIdentity"/>; does not
/// invoke <c>sudo</c>/<c>pkexec</c> on the operator's behalf.
/// </summary>
public sealed class PosixElevationPal : IElevationPal
{
    public bool IsElevated => PosixIdentity.IsRoot;

    public ElevationResult Relaunch(string exe, IReadOnlyList<string> args) =>
        ElevationResult.Unsupported();

    public ElevationResult RelaunchAndWait(string exe, IReadOnlyList<string> args) =>
        ElevationResult.Unsupported();
}
