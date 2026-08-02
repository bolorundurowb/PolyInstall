using Microsoft.Extensions.FileSystemGlobbing;

namespace PolyInstall.Core.Build.Globbing;

/// <summary>
/// Represents a file matched by a glob pattern.
/// </summary>
/// <param name="RelativePath">The path relative to the source directory, normalized to forward slashes.</param>
/// <param name="FullPath">The absolute path to the file on disk.</param>
public sealed record GlobbedFile(string RelativePath, string FullPath);

/// <summary>
/// Provides methods to resolve file system globs.
/// </summary>
public static class GlobResolver
{
    /// <summary>
    /// Resolves include and exclude glob patterns under a source directory.
    /// </summary>
    /// <param name="baseDirectory">The base directory for resolving relative paths.</param>
    /// <param name="sourceDir">The directory to search for files, absolute or relative to <paramref name="baseDirectory"/>.</param>
    /// <param name="include">A list of glob patterns to include.</param>
    /// <param name="exclude">An optional list of glob patterns to exclude.</param>
    /// <returns>A sorted list of <see cref="GlobbedFile"/> objects.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if the resolved source directory does not exist.</exception>
    public static IReadOnlyList<GlobbedFile> Collect(
        string baseDirectory,
        string sourceDir,
        IEnumerable<string> include,
        IEnumerable<string>? exclude)
    {
        var fullSource = Path.GetFullPath(Path.Combine(baseDirectory, sourceDir));
        if (!Directory.Exists(fullSource))
            throw new DirectoryNotFoundException($"Source directory not found: {fullSource}");

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var g in include)
            matcher.AddInclude(g.Trim());
        if (exclude is not null)
            foreach (var g in exclude)
                matcher.AddExclude(g.Trim());

        var results = new List<GlobbedFile>();
        foreach (var file in matcher.GetResultsInFullPath(fullSource))
        {
            if (!File.Exists(file) || !IsPathWithinDirectory(fullSource, file))
                continue;

            var rel = Path.GetRelativePath(fullSource, file);
            var normalized = rel.Replace('\\', '/');
            results.Add(new GlobbedFile(normalized, file));
        }

        results.Sort((a, b) => string.CompareOrdinal(a.RelativePath, b.RelativePath));
        return results;
    }

    private static bool IsPathWithinDirectory(string directory, string candidatePath)
    {
        var root = Path.GetFullPath(directory);
        var full = Path.GetFullPath(candidatePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var last = root[^1];
        if (last != Path.DirectorySeparatorChar && last != Path.AltDirectorySeparatorChar)
            root += Path.DirectorySeparatorChar;
        return full.StartsWith(root, comparison);
    }
}
