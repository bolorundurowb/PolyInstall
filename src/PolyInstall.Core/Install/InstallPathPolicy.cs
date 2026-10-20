namespace PolyInstall.Install;

/// <summary>
/// Decides whether a directory is an unsafe install or uninstall root. Blocks volume roots,
/// well-known system and profile directories (exact match), anything underneath the Windows
/// system directories, and ancestors of those directories (for example <c>C:\Users</c>).
/// Subdirectories of Program Files and of the user profile stay allowed, because those are
/// the normal per-machine and per-user install locations.
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
            if (root.Equals(path, comparison))
                return true;

            // The candidate contains a sensitive directory (for example C:\Users).
            if (path!.StartsWith(root + Path.DirectorySeparatorChar, comparison))
                return true;
        }

        foreach (var sysRoot in SystemRoots().Select(NormalizeOrNull).Where(path => !string.IsNullOrEmpty(path)))
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
