using System.IO.Compression;
using System.Text;
using PolyInstall.Install;

namespace PolyInstall.Core.Tests;

public class ZipPayloadExtractorTests
{
    [Fact]
    public void ExtractToDirectory_WithValidZip_ExtractsFilesToDestination()
    {
        var dest = TestHelpers.NewTempDir();
        try
        {
            var zip = CreateZipBytes([("a.txt", "hello"), ("sub/b.txt", "world")]);

            ZipPayloadExtractor.ExtractToDirectory(zip, dest);

            File.ReadAllText(Path.Combine(dest, "a.txt")).Must().Be("hello");
            File.ReadAllText(Path.Combine(dest, "sub", "b.txt")).Must().Be("world");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dest);
        }
    }

    [Fact]
    public void ExtractToDirectory_WithDirectoryOnlyEntries_CreatesDirectoriesAndExtractsFiles()
    {
        var dest = TestHelpers.NewTempDir();
        try
        {
            var zip = CreateZipBytes([("empty/", ""), ("a.txt", "x")]);

            ZipPayloadExtractor.ExtractToDirectory(zip, dest);

            File.Exists(Path.Combine(dest, "a.txt")).Must().BeTrue();
            Directory.Exists(Path.Combine(dest, "empty")).Must().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dest);
        }
    }

    [Fact]
    public void ExtractToDirectory_WithPathTraversalZipEntry_ThrowsInvalidOperationException()
    {
        var dest = TestHelpers.NewTempDir();
        try
        {
            var zip = CreateZipWithTraversalEntry();

            ((Action)(() => ZipPayloadExtractor.ExtractToDirectory(zip, dest))).Throws<InvalidOperationException>()
                .WithMessageContaining("escapes destination");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dest);
        }
    }

    [Fact]
    public void ExtractToDirectory_WithEmptyDirectoryEntry_CreatesDirectory()
    {
        var dest = TestHelpers.NewTempDir();
        try
        {
            var zip = CreateZipWithEmptyDirectory();

            ZipPayloadExtractor.ExtractToDirectory(zip, dest);

            Directory.Exists(Path.Combine(dest, "empty_cache")).Must().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dest);
        }
    }

    [Fact]
    public void ExtractToDirectory_WithNestedEmptyDirectory_CreatesNestedDirectory()
    {
        var dest = TestHelpers.NewTempDir();
        try
        {
            var zip = CreateZipWithNestedEmptyDirectory();

            ZipPayloadExtractor.ExtractToDirectory(zip, dest);

            Directory.Exists(Path.Combine(dest, "parent", "child")).Must().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dest);
        }
    }

    private static byte[] CreateZipBytes(IEnumerable<(string Name, string Content)> entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                if (name.EndsWith('/'))
                {
                    zip.CreateEntry(name);
                }
                else
                {
                    var e = zip.CreateEntry(name);
                    using var w = new StreamWriter(e.Open());
                    w.Write(content);
                }
            }
        }

        return ms.ToArray();
    }

    private static byte[] CreateZipWithTraversalEntry()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Manually craft an entry with ../ traversal
            var e = zip.CreateEntry("../../outside.txt");
            using var w = new StreamWriter(e.Open());
            w.Write("bad");
        }

        return ms.ToArray();
    }

    private static byte[] CreateZipWithEmptyDirectory()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("empty_cache/");
        }

        return ms.ToArray();
    }

    private static byte[] CreateZipWithNestedEmptyDirectory()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("parent/");
            zip.CreateEntry("parent/child/");
        }

        return ms.ToArray();
    }
}
