namespace PolyInstall.Pal;

/// <summary>
/// Pure path-boundary matching used to decide whether an executable image lives under an
/// install destination. Kept separate from <see cref="IProcessManagerPal"/> so the matching
/// logic can be unit tested without enumerating real processes.
/// </summary>
public static class ProcessPathMatcher
{
    // Executable and directory comparisons are case-insensitive to match the plan's
    // "case-insensitive, normalized full paths" behavior across platforms.
    private const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// Returns true when <paramref name="executablePath"/> resolves to a location strictly under
    /// <paramref name="directory"/>. Uses a path-boundary check so that <c>C:\App</c> does not
    /// match a sibling <c>C:\AppExtra</c>. Returns false for empty/blank inputs or paths that
    /// cannot be normalized.
    /// </summary>
    public static bool IsUnderDirectory(string? executablePath, string? directory)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || string.IsNullOrWhiteSpace(directory))
            return false;

        string fullExe;
        string fullDir;
        try
        {
            fullExe = Path.GetFullPath(executablePath);
            fullDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        }
        catch
        {
            return false;
        }

        if (string.IsNullOrEmpty(fullDir))
            return false;

        // The executable must live *under* the directory, not be the directory itself.
        var prefix = fullDir + Path.DirectorySeparatorChar;
        return fullExe.StartsWith(prefix, Comparison);
    }
}
