using System.IO.Compression;
using System.Text;
using PolyInstall.Payload;

namespace PolyInstall.Core.Tests;

public class PayloadArchiveTests
{
    [Theory]
    [InlineData("lzma")]
    [InlineData("xz")]
    [InlineData("deflate")]
    public void ParseCompression_WithUnsupportedName_ThrowsArgumentException(string name)
    {
        ((Action)(() => PayloadArchive.ParseCompression(name))).Throws<ArgumentException>()
            .WithMessageContaining("brotli")
            .WithMessageContaining("gzip");
    }

    [Theory]
    [InlineData("brotli")]
    [InlineData("gzip")]
    [InlineData("Brotli")]
    public void ParseCompression_WithSupportedName_DoesNotThrow(string name)
    {
        ((Action)(() => PayloadArchive.ParseCompression(name))).NotThrow();
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
            entry.Verify().NotToBeNull();
            using var sr = new StreamReader(entry!.Open());
            sr.ReadToEnd().Verify().ToBe("hello world");
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
            zip.Entries.Select(e => e.FullName).Verify().ToBeEquivalentTo(["a.txt", "b.txt"]);
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
            zip.Entries.Select(e => e.FullName).Verify().ToContain("dir/file.txt");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData(PayloadCompression.Brotli)]
    [InlineData(PayloadCompression.GZip)]
    public void PackAndCompressToFile_WithSingleFile_WritesCompressedPayload(PayloadCompression compression)
    {
        var dir = TestHelpers.NewTempDir();
        var payloadPath = Path.Combine(dir, "payload.bin");
        try
        {
            var sourcePath = Path.Combine(dir, "app.txt");
            File.WriteAllText(sourcePath, "streamed payload");

            var length = PayloadArchive.PackAndCompressToFile(
                [("app.txt", sourcePath)],
                compression,
                payloadPath);

            length.Verify().ToBeGreaterThan(0);
            new FileInfo(payloadPath).Length.Verify().ToBe(length);

            var decompressed = PayloadArchive.Decompress(File.ReadAllBytes(payloadPath), compression);
            using var ms = new MemoryStream(decompressed);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var sr = new StreamReader(zip.GetEntry("app.txt")!.Open());
            sr.ReadToEnd().Verify().ToBe("streamed payload");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dir);
        }
    }
}
