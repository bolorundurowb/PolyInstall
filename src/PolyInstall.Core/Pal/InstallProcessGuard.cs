namespace PolyInstall.Pal;

/// <summary>
/// Composition helpers for the "running app" install gate: terminate the processes the user
/// consented to close, then re-scan the destination to confirm the binaries are no longer
/// locked before the copy phase begins.
/// </summary>
public static class InstallProcessGuard
{
    /// <summary>
    /// Terminates the given processes via <paramref name="processes"/> and returns any processes
    /// still running under <paramref name="directory"/> afterward. An empty result means the
    /// destination is clear and the install may proceed.
    /// </summary>
    public static IReadOnlyList<RunningProcessInfo> TerminateAndRescan(
        IProcessManagerPal processes,
        IEnumerable<RunningProcessInfo> toTerminate,
        string directory)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(toTerminate);

        processes.Terminate(toTerminate.Select(p => p.Id), directory);
        return processes.FindProcessesUnderDirectory(directory);
    }
}
