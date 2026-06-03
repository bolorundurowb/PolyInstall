using System.IO.Compression;
using System.Text;
using PolyInstall.Core.Payload;

namespace PolyInstall.Core.Tests;

public class PayloadArchiveTests
{
    [Theory]
    [InlineData("lzma")]
    [InlineData("xz")]
    [InlineData("deflate")]
    public void ParseCompression_WithUnsupportedName_ThrowsArgumentException(string name)
    {
        FluentActions.Invoking(() => PayloadArchive.ParseCompression(name))
            .Should().Throw<ArgumentException>()
            .WithMessage("*brotli*gzip*");
    }

    [Theory]
    [InlineData("brotli")]
    [InlineData("gzip")]
    [InlineData("Brotli")]
    public void ParseCompression_WithSupportedName_DoesNotThrow(string name)
    {
        FluentActions.Invoking(() => PayloadArchive.ParseCompression(name)).Should().NotThrow();
    }

    [Theory]
    [InlineData(PayloadCompression.Brotli)]
    [InlineData(PayloadCompression.GZip)]
    public void PackAndCompress_WithSingleFile_RoundTripsSuccessfully(PayloadCompression compression)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".txt");
        File.WriteAllText(tempFile, "hello world");
        try
        {
            var compressed = PayloadArchive.PackAndCompress(
                [("files/test.txt", tempFile)],
                compression);

            var decompressed = PayloadArchive.Decompress(compressed, compression);

            using var ms = new MemoryStream(decompressed);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("files/test.txt");
            entry.Should().NotBeNull();
            using var sr = new StreamReader(entry!.Open());
            sr.ReadToEnd().Should().Be("hello world");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData(PayloadCompression.Brotli)]
    [InlineData(PayloadCompression.GZip)]
    public void PackAndCompress_WithMultipleFiles_IncludesAllEntries(PayloadCompression compression)
    {
        var dir = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.txt"), "A");
            File.WriteAllText(Path.Combine(dir, "b.txt"), "B");

            var compressed = PayloadArchive.PackAndCompress(
                [("a.txt", Path.Combine(dir, "a.txt")), ("b.txt", Path.Combine(dir, "b.txt"))],
                compression);

            var decompressed = PayloadArchive.Decompress(compressed, compression);
            using var ms = new MemoryStream(decompressed);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            zip.Entries.Select(e => e.FullName).Should().BeEquivalentTo("a.txt", "b.txt");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public void PackAndCompress_WithBackslashEntryName_NormalizesToForwardSlash()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("n") + ".txt");
        File.WriteAllText(tempFile, "x");
        try
        {
            var compressed = PayloadArchive.PackAndCompress(
                [("dir\\file.txt", tempFile)],
                PayloadCompression.GZip);

            var decompressed = PayloadArchive.Decompress(compressed, PayloadCompression.GZip);
            using var ms = new MemoryStream(decompressed);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            zip.Entries.Select(e => e.FullName).Should().Contain("dir/file.txt");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
