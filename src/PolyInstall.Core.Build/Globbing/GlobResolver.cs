using Microsoft.Extensions.FileSystemGlobbing;

namespace PolyInstall.Core.Globbing;

public sealed record GlobbedFile(string RelativePath, string FullPath);

public static class GlobResolver
{
    /// <summary>
    /// Resolves <paramref name="include"/> / <paramref name="exclude"/> under <paramref name="sourceDir"/> (absolute or relative to <paramref name="baseDirectory"/>).
    /// </summary>
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
