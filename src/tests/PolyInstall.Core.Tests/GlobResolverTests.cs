using PolyInstall.Core.Globbing;

namespace PolyInstall.Core.Tests;

/// <summary>
/// Tests for <see cref="GlobResolver.Collect"/>.
/// Each test creates its own isolated temp directory and cleans it up in a finally block.
/// </summary>
public class GlobResolverTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>Creates a unique temp directory, writes the given relative paths into it,
    /// and returns its full path.</summary>
    private static string MakeTempTree(params string[] relativePaths)
    {
        var root = Path.Combine(Path.GetTempPath(), "polyinstall-globtest-" + Guid.NewGuid().ToString("n"));
        // Always create the root so callers get a valid (possibly empty) directory.
        Directory.CreateDirectory(root);
        foreach (var rel in relativePaths)
        {
            var full = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, rel);
        }
        return root;
    }

    private static void DeleteTree(string root)
    {
        try { Directory.Delete(root, recursive: true); } catch { /* best-effort */ }
    }

    // -------------------------------------------------------------------------
    // Basic behaviour
    // -------------------------------------------------------------------------

    [Fact]
    public void Collect_ReturnsAllMatchingFiles_ForRecursiveGlob()
    {
        var root = MakeTempTree("a.txt", "sub/b.txt", "sub/deep/c.txt");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*.txt"], null);
            results.Select(f => f.RelativePath).Should().BeEquivalentTo(
                "a.txt", "sub/b.txt", "sub/deep/c.txt");
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_RelativePaths_UseForwardSlashes()
    {
        var root = MakeTempTree("dir/nested/file.dat");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*.dat"], null);
            results.Should().ContainSingle();
            results[0].RelativePath.Should().Be("dir/nested/file.dat");
            results[0].RelativePath.Should().NotContain("\\");
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_FullPath_PointsToExistingFile()
    {
        var root = MakeTempTree("hello.bin");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*"], null);
            results.Should().ContainSingle();
            File.Exists(results[0].FullPath).Should().BeTrue();
        }
        finally { DeleteTree(root); }
    }

    // -------------------------------------------------------------------------
    // Exclude patterns
    // -------------------------------------------------------------------------

    [Fact]
    public void Collect_ExcludePattern_FiltersMatchingFiles()
    {
        var root = MakeTempTree("keep.txt", "skip.log", "sub/also-skip.log", "sub/keep2.txt");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*"], ["**/*.log"]);
            results.Select(f => f.RelativePath).Should().BeEquivalentTo("keep.txt", "sub/keep2.txt");
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_ExcludeDirectory_FiltersAllFilesUnderIt()
    {
        var root = MakeTempTree("include.txt", "excluded/a.txt", "excluded/b.txt");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*"], ["excluded/**"]);
            results.Should().HaveCount(1);
            results[0].RelativePath.Should().Be("include.txt");
        }
        finally { DeleteTree(root); }
    }

    // -------------------------------------------------------------------------
    // Deterministic ordering
    // -------------------------------------------------------------------------

    [Fact]
    public void Collect_ResultsAreSortedByRelativePath()
    {
        // Create in reverse alphabetical order to ensure sorting is applied.
        var root = MakeTempTree("z.txt", "m.txt", "a.txt", "sub/z.txt", "sub/a.txt");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*.txt"], null);
            var paths = results.Select(f => f.RelativePath).ToList();
            var sorted = paths.OrderBy(p => p, StringComparer.Ordinal).ToList();
            paths.Should().Equal(sorted, "results must be sorted in ordinal ascending order");
        }
        finally { DeleteTree(root); }
    }

    // -------------------------------------------------------------------------
    // Source directory resolution
    // -------------------------------------------------------------------------

    [Fact]
    public void Collect_SourceDirRelativeToBase_ResolvesCorrectly()
    {
        // Structure: <root>/src/app.cs  — base is <root>, sourceDir is "src"
        var root = MakeTempTree("src/app.cs", "other/readme.txt");
        try
        {
            var results = GlobResolver.Collect(root, "src", ["**/*.cs"], null);
            results.Should().ContainSingle();
            results[0].RelativePath.Should().Be("app.cs");
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_EmptyDirectory_ReturnsEmpty()
    {
        var root = MakeTempTree(); // no files
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*"], null);
            results.Should().BeEmpty();
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_NoFileMatchesPattern_ReturnsEmpty()
    {
        var root = MakeTempTree("file.txt");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*.exe"], null);
            results.Should().BeEmpty();
        }
        finally { DeleteTree(root); }
    }

    // -------------------------------------------------------------------------
    // Multiple include patterns
    // -------------------------------------------------------------------------

    [Fact]
    public void Collect_MultipleIncludePatterns_UnionResults()
    {
        var root = MakeTempTree("a.txt", "b.dll", "c.png", "d.log");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*.txt", "**/*.dll"], null);
            results.Select(f => f.RelativePath).Should().BeEquivalentTo("a.txt", "b.dll");
        }
        finally { DeleteTree(root); }
    }

    // -------------------------------------------------------------------------
    // Error cases
    // -------------------------------------------------------------------------

    [Fact]
    public void Collect_NonExistentSourceDir_ThrowsDirectoryNotFoundException()
    {
        var root = MakeTempTree("dummy.txt");
        try
        {
            var act = () => GlobResolver.Collect(root, "does-not-exist", ["**/*"], null);
            act.Should().Throw<DirectoryNotFoundException>()
                .WithMessage("*does-not-exist*");
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_EmptyIncludeList_ReturnsEmpty()
    {
        var root = MakeTempTree("file.txt");
        try
        {
            // No patterns → Matcher with no includes matches nothing
            var results = GlobResolver.Collect(root, ".", [], null);
            results.Should().BeEmpty();
        }
        finally { DeleteTree(root); }
    }

    // -------------------------------------------------------------------------
    // Path-traversal safety
    // -------------------------------------------------------------------------

    [Fact]
    public void Collect_GlobPatternCannotEscapeSourceDir()
    {
        // A crafted pattern containing ".." should still only return files
        // inside (or at) the resolved source directory. The FileSystemGlobbing
        // Matcher doesn't traverse above its root, so the result must be empty
        // or limited to the source dir — never files from outside.
        var root = MakeTempTree("inside.txt");
        // Place a sentinel file one level above so we can assert it was NOT matched.
        var sentinel = Path.Combine(Path.GetTempPath(), "polyinstall-sentinel-" + Guid.NewGuid().ToString("n") + ".txt");
        File.WriteAllText(sentinel, "should-not-appear");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["../*.txt"], null);
            var paths = results.Select(f => f.FullPath).ToList();
            paths.Should().NotContain(sentinel, "glob patterns must not escape the source directory");
        }
        finally
        {
            DeleteTree(root);
            try { File.Delete(sentinel); } catch { /* ignore */ }
        }
    }
}
