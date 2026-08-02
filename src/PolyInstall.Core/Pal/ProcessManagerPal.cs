using System.Diagnostics;

namespace PolyInstall.Pal;

/// <summary>
/// Default <see cref="IProcessManagerPal"/> implementation backed by
/// <see cref="System.Diagnostics.Process"/>. Best-effort on all platforms; Windows is the
/// primary case where a locked EXE/DLL breaks an overwrite install.
/// </summary>
public sealed class ProcessManagerPal : IProcessManagerPal
{
    public IReadOnlyList<RunningProcessInfo> FindProcessesUnderDirectory(string directory)
    {
        var matches = new List<RunningProcessInfo>();
        if (string.IsNullOrWhiteSpace(directory))
            return matches;

        string fullDir;
        try
        {
            fullDir = Path.GetFullPath(directory);
        }
        catch
        {
            return matches;
        }

        if (!Directory.Exists(fullDir))
            return matches;

        var self = Environment.ProcessId;
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == self)
                    continue;

                string? imagePath;
                try
                {
                    // Access-denied (system/elevated processes) and exited-process races
                    // both surface here; skip such processes rather than failing the scan.
                    imagePath = process.MainModule?.FileName;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(imagePath))
                    continue;

                if (ProcessPathMatcher.IsUnderDirectory(imagePath, fullDir))
                    matches.Add(new RunningProcessInfo(process.Id, SafeName(process), imagePath));
            }
            catch
            {
                // Ignore any per-process failure and continue scanning.
            }
            finally
            {
                process.Dispose();
            }
        }

        return matches;
    }

    public void Terminate(IEnumerable<int> processIds, string mustBeUnderDirectory)
    {
        var failures = new List<string>();
        foreach (var id in processIds)
        {
            try
            {
                using var process = Process.GetProcessById(id);

                // Re-validate right before killing: the PID discovered earlier may have been
                // recycled for an unrelated process since the user gave consent.
                string? imagePath;
                try
                {
                    imagePath = process.MainModule?.FileName;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrEmpty(imagePath)
                    || !ProcessPathMatcher.IsUnderDirectory(imagePath, mustBeUnderDirectory))
                {
                    continue;
                }

                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (ArgumentException)
            {
                // No such process (already exited) — nothing to terminate.
            }
            catch (InvalidOperationException)
            {
                // Process has already exited between discovery and kill.
            }
            catch (Exception ex)
            {
                failures.Add($"PID {id}: {ex.Message}");
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                "Failed to terminate one or more processes: " + string.Join("; ", failures));
    }

    private static string SafeName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch
        {
            return "(unknown)";
        }
    }
}
