namespace PolyInstall.Install;

/// <summary>
/// Best-effort cleanup after a cancelled fresh install that already touched disk.
/// Shared by the Avalonia wizard and the silent runner.
/// </summary>
public static class PartialInstallCleanup
{
    public static void Run(
        string installDirectory,
        string extractRoot,
        bool createdInstallDirectory)
    {
        if (createdInstallDirectory)
        {
            TryDeleteDirectoryRecursive(installDirectory);
            return;
        }

        TryCleanupArpRegistration(installDirectory);
        RemoveCopiedPayloadFiles(extractRoot, installDirectory);
        TryDeleteFile(InstallStatePaths.UninstallExePath(installDirectory));
        TryDeleteDirectoryRecursive(InstallStatePaths.PolyDir(installDirectory));
        TryDeleteEmptyDirectoriesForPayload(extractRoot, installDirectory);
    }

    private static void RemoveCopiedPayloadFiles(string sourceRoot, string destRoot)
    {
        if (!Directory.Exists(sourceRoot) || !Directory.Exists(destRoot))
            return;

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, sourceFile);
            var destFile = Path.Combine(destRoot, relative);
            TryDeleteFile(destFile);
        }
    }

    private static void TryDeleteEmptyDirectoriesForPayload(string sourceRoot, string destRoot)
    {
        if (!Directory.Exists(sourceRoot) || !Directory.Exists(destRoot))
            return;

        var sourceDirs = Directory.EnumerateDirectories(sourceRoot, "*", SearchOption.AllDirectories)
            .OrderByDescending(static d => d.Length)
            .Select(sourceDir => Path.Combine(destRoot, Path.GetRelativePath(sourceRoot, sourceDir)));
        foreach (var dir in sourceDirs)
            TryDeleteEmptyDirectory(dir);
    }

    private static void TryDeleteDirectoryRecursive(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).FirstOrDefault() is null)
                Directory.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void TryCleanupArpRegistration(string installDirectory)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            var statePath = InstallStatePaths.InstallStatePath(installDirectory);
            if (!File.Exists(statePath))
                return;
            var state = InstallStateIo.ReadState(installDirectory);
#pragma warning disable CA1416 // Guarded by OperatingSystem.IsWindows()
            WindowsArpRegistration.Unregister(state);
#pragma warning restore CA1416
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
