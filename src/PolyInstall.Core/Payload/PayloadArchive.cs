using System.IO.Compression;

namespace PolyInstall.Payload;

public enum PayloadCompression
{
    Brotli,
    GZip,
}

public static class PayloadArchive
{
    public static PayloadCompression ParseCompression(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "brotli" => PayloadCompression.Brotli,
            "gzip" => PayloadCompression.GZip,
            _ => throw new ArgumentException($"Unsupported compression: '{name}'. Use 'brotli' or 'gzip'.", nameof(name)),
        };
    }

    /// <summary>
    /// Builds a zip in memory from files, then compresses with Brotli or GZip.
    /// Prefer <see cref="PackAndCompressToFile"/> for production builds.
    /// </summary>
    public static byte[] PackAndCompress(IEnumerable<(string EntryName, string FullPath)> files, PayloadCompression compression, CancellationToken ct = default)
    {
        using var zipMs = new MemoryStream();
        using (var zip = new ZipArchive(zipMs, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (entryName, fullPath) in files)
            {
                ct.ThrowIfCancellationRequested();
                var e = zip.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Optimal);
                using var dest = e.Open();
                using var src = File.OpenRead(fullPath);
                src.CopyTo(dest);
            }
        }

        var raw = zipMs.ToArray();
        return compression switch
        {
            PayloadCompression.Brotli => CompressBrotli(raw, ct),
            PayloadCompression.GZip => CompressGZip(raw, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(compression)),
        };
    }

    /// <summary>
    /// Streams a zipped payload through the configured compression into <paramref name="outputPath"/>.
    /// </summary>
    public static long PackAndCompressToFile(
        IEnumerable<(string EntryName, string FullPath)> files,
        PayloadCompression compression,
        string outputPath,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using (var output = File.Create(outputPath))
        using (var compressed = CreateCompressionStream(output, compression))
        using (var zip = new ZipArchive(compressed, ZipArchiveMode.Create))
        {
            foreach (var (entryName, fullPath) in files)
            {
                ct.ThrowIfCancellationRequested();
                var e = zip.CreateEntry(entryName.Replace('\\', '/'), CompressionLevel.Optimal);
                using var dest = e.Open();
                using var src = File.OpenRead(fullPath);
                CopyToWithCancellation(src, dest, ct);
            }
        }

        return new FileInfo(outputPath).Length;
    }

    public static byte[] Decompress(byte[] compressed, PayloadCompression compression, CancellationToken ct = default)
    {
        return compression switch
        {
            PayloadCompression.Brotli => DecompressBrotli(compressed, ct),
            PayloadCompression.GZip => DecompressGZip(compressed, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(compression)),
        };
    }

    private static byte[] CompressBrotli(byte[] raw, CancellationToken ct)
    {
        using var input = new MemoryStream(raw);
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
            CopyToWithCancellation(input, brotli, ct);
        return output.ToArray();
    }

    private static byte[] DecompressBrotli(byte[] compressed, CancellationToken ct)
    {
        using var input = new MemoryStream(compressed);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        InstallPayloadLimits.CopyWithLimit(brotli, output, InstallPayloadLimits.MaxDecompressedPayloadBytes, ct);
        return output.ToArray();
    }

    private static byte[] CompressGZip(byte[] raw, CancellationToken ct)
    {
        using var input = new MemoryStream(raw);
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            CopyToWithCancellation(input, gz, ct);
        return output.ToArray();
    }

    private static byte[] DecompressGZip(byte[] compressed, CancellationToken ct)
    {
        using var input = new MemoryStream(compressed);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        InstallPayloadLimits.CopyWithLimit(gz, output, InstallPayloadLimits.MaxDecompressedPayloadBytes, ct);
        return output.ToArray();
    }

    private static Stream CreateCompressionStream(Stream output, PayloadCompression compression)
    {
        return compression switch
        {
            PayloadCompression.Brotli => new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true),
            PayloadCompression.GZip => new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true),
            _ => throw new ArgumentOutOfRangeException(nameof(compression)),
        };
    }

    private static void CopyToWithCancellation(Stream source, Stream destination, CancellationToken ct)
    {
        var buffer = new byte[1024 * 128];
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            destination.Write(buffer, 0, read);
        }
    }
}
