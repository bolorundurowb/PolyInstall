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

            File.ReadAllText(Path.Combine(dest, "a.txt")).Should().Be("hello");
            File.ReadAllText(Path.Combine(dest, "sub", "b.txt")).Should().Be("world");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dest);
        }
    }

    [Fact]
    public void ExtractToDirectory_WithDirectoryOnlyEntries_SkipsDirectoriesWithoutFiles()
    {
        var dest = TestHelpers.NewTempDir();
        try
        {
            // Directory-only entries have empty Name; extractor intentionally skips them
            var zip = CreateZipBytes([("empty/", ""), ("a.txt", "x")]);

            ZipPayloadExtractor.ExtractToDirectory(zip, dest);

            File.Exists(Path.Combine(dest, "a.txt")).Should().BeTrue();
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

            FluentActions.Invoking(() => ZipPayloadExtractor.ExtractToDirectory(zip, dest))
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*escapes destination*");
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
}
