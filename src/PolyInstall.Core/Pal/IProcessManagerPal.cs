namespace PolyInstall.Pal;

/// <summary>
/// Describes a running OS process whose executable image was resolved on disk.
/// </summary>
public sealed record RunningProcessInfo(int Id, string Name, string ExecutablePath);

/// <summary>
/// Platform abstraction for discovering and terminating running processes whose executable
/// lives under a given directory. Used to gate installs when the target binaries are locked
/// by a running instance of the product being installed/updated.
/// </summary>
public interface IProcessManagerPal
{
    /// <summary>
    /// Returns processes whose executable image path is located under <paramref name="directory"/>.
    /// The current (installer) process is always excluded. Returns an empty list when the
    /// directory is empty, missing, or no matching processes are running.
    /// </summary>
    IReadOnlyList<RunningProcessInfo> FindProcessesUnderDirectory(string directory);

    /// <summary>
    /// Attempts to terminate the given processes (and their child process trees where
    /// supported). Before each kill the process image path is re-resolved and the PID is
    /// skipped when it no longer maps to an executable under
    /// <paramref name="mustBeUnderDirectory"/> (PID reuse between discovery and termination).
    /// Processes that have already exited are ignored; unexpected failures are aggregated
    /// into an <see cref="InvalidOperationException"/>.
    /// </summary>
    void Terminate(IEnumerable<int> processIds, string mustBeUnderDirectory);
}
