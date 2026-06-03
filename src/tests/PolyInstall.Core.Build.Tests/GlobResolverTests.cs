using PolyInstall.Core.Build.Globbing;

namespace PolyInstall.Core.Build.Tests;

public class GlobResolverTests
{
    private static string MakeTempTree(params string[] relativePaths)
    {
        var root = Path.Combine(Path.GetTempPath(), "polyinstall-globtest-" + Guid.NewGuid().ToString("n"));
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
        try { Directory.Delete(root, recursive: true); } catch { }
    }

    [Fact]
    public void Collect_WithNestedTextFiles_ReturnsRelativePaths()
    {
        var root = MakeTempTree("a.txt", "sub/b.txt");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*.txt"], null);
            results.Should().HaveCount(2);
            results.Select(f => f.RelativePath).Should().BeEquivalentTo("a.txt", "sub/b.txt");
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_WithRecursiveGlob_ReturnsAllMatchingTextFiles()
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
    public void Collect_WithNestedFiles_ReturnsForwardSlashRelativePaths()
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
    public void Collect_WhenMatchesExist_ReturnsAccessibleFullPaths()
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

    [Fact]
    public void Collect_WithExcludePatterns_OmitsMatchingFiles()
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
    public void Collect_WithExcludedDirectory_OmitsSubtree()
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

    [Fact]
    public void Collect_WithManyFiles_ReturnsSortedRelativePaths()
    {
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

    [Fact]
    public void Collect_WithRelativeSourceDir_MatchesUnderResolvedDirectory()
    {
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
    public void Collect_WithEmptyDirectory_ReturnsEmpty()
    {
        var root = MakeTempTree();
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*"], null);
            results.Should().BeEmpty();
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_WhenNothingMatchesPattern_ReturnsEmpty()
    {
        var root = MakeTempTree("file.txt");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*.exe"], null);
            results.Should().BeEmpty();
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_WithMultipleIncludePatterns_ReturnsUnion()
    {
        var root = MakeTempTree("a.txt", "b.dll", "c.png", "d.log");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*.txt", "**/*.dll"], null);
            results.Select(f => f.RelativePath).Should().BeEquivalentTo("a.txt", "b.dll");
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_WithMissingSourceDirectory_ThrowsDirectoryNotFoundException()
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
    public void Collect_WithEmptyIncludeList_ReturnsEmpty()
    {
        var root = MakeTempTree("file.txt");
        try
        {
            var results = GlobResolver.Collect(root, ".", [], null);
            results.Should().BeEmpty();
        }
        finally { DeleteTree(root); }
    }

    [Fact]
    public void Collect_WithParentRelativeIncludePattern_ReturnsOnlyFilesUnderSourceDirectory()
    {
        var root = MakeTempTree("inside.txt");
        try
        {
            var results = GlobResolver.Collect(root, ".", ["**/*.txt", "../*.txt"], null);
            results.Should().ContainSingle();
            results[0].RelativePath.Should().Be("inside.txt");
        }
        finally { DeleteTree(root); }
    }
}
