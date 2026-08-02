namespace PolyInstall.Install;

/// <summary>
/// Runtime enforcement that manifest-supplied names/relative paths (shortcut names,
/// subfolders, desktop entry file names, …) cannot escape their intended base directory.
/// Build-time validation is not a security boundary: unsigned or patched installers can
/// carry manifests that never went through the CLI, so these checks are re-applied at
/// execution time.
/// </summary>
public static class RelativePathGuard
{
    /// <summary>A single path segment: no separators, not rooted, no traversal.</summary>
    public static bool IsSimpleFileName(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !Path.IsPathRooted(value)
        && !value.Contains('/')
        && !value.Contains('\\')
        && !value.Contains("..", StringComparison.Ordinal)
        && value is not "." and not "..";

    /// <summary>A relative path (possibly nested) that cannot traverse above its base.</summary>
    public static bool IsSimpleRelativePath(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !Path.IsPathRooted(value)
        && !value.Contains("..", StringComparison.Ordinal);

    public static void EnsureSimpleFileName(string? value, string description)
    {
        if (!IsSimpleFileName(value))
            throw new InvalidOperationException(
                $"{description} must be a simple file name without path separators or '..', got '{value}'.");
    }

    public static void EnsureSimpleRelativePath(string? value, string description)
    {
        if (!IsSimpleRelativePath(value))
            throw new InvalidOperationException(
                $"{description} must be a relative path without root or '..' segments, got '{value}'.");
    }

    /// <summary>
    /// Combines <paramref name="segments"/> under <paramref name="baseDir"/> and throws when
    /// the resolved path would land outside <paramref name="baseDir"/>.
    /// </summary>
    public static string CombineConfined(string baseDir, params string[] segments)
    {
        var fullBase = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDir));
        var combined = Path.GetFullPath(Path.Combine([fullBase, .. segments]));
        var comparison = PathComparison;
        if (!combined.StartsWith(fullBase + Path.DirectorySeparatorChar, comparison)
            && !combined.Equals(fullBase, comparison))
        {
            throw new InvalidOperationException(
                $"Path '{combined}' escapes its intended base directory '{fullBase}'.");
        }

        return combined;
    }

    /// <summary>Path comparison matching platform file-system semantics.</summary>
    public static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
