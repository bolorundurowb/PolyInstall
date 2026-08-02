namespace PolyInstall.Install;

/// <summary>
/// Policy deciding whether a directory is an unsafe install/uninstall root. Blocks volume
/// roots, well-known system/profile directories (exact match), directories underneath the
/// Windows system directories, and ancestors of well-known directories (e.g. <c>C:\Users</c>).
/// Subdirectories of Program Files / the user profile remain allowed since those are the
/// normal per-machine / per-user install locations.
/// </summary>
public static class InstallPathPolicy
{
    public static bool IsDangerousInstallRoot(string installRoot)
    {
        string root;
        try
        {
            root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));
        }
        catch
        {
            return true;
        }

        if (root.Length == 0)
            return true;

        var comparison = RelativePathGuard.PathComparison;
        var volumeRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root) ?? "");
        if (!string.IsNullOrEmpty(volumeRoot)
            && root.Equals(volumeRoot, comparison))
        {
            return true;
        }

        var dangerous = DangerousRoots()
            .Select(NormalizeOrNull)
            .Where(path => !string.IsNullOrEmpty(path))
            .ToList();

        foreach (var path in dangerous)
        {
            // Exact match (e.g. C:\Windows, C:\Program Files, the profile root itself).
            if (root.Equals(path, comparison))
                return true;

            // The candidate is an ancestor of a sensitive directory (e.g. C:\Users).
            if (path!.StartsWith(root + Path.DirectorySeparatorChar, comparison))
                return true;
        }

        // Nothing may install underneath the OS system directories.
        foreach (var sysRoot in SystemRoots().Select(NormalizeOrNull).Where(p => !string.IsNullOrEmpty(p)))
        {
            if (root.StartsWith(sysRoot + Path.DirectorySeparatorChar, comparison))
                return true;
        }

        return false;
    }

    public static void EnsureSafeInstallRoot(string installRoot)
    {
        if (IsDangerousInstallRoot(installRoot))
            throw new InvalidOperationException(
                $"Refusing to use unsafe install directory: {installRoot}");
    }

    private static string? NormalizeOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> DangerousRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.System);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    }

    private static IEnumerable<string> SystemRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.System);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
    }
}
