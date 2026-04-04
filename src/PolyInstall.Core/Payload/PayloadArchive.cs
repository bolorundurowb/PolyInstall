using System.IO.Compression;

namespace PolyInstall.Core.Payload;

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
            input.CopyTo(brotli);
        ct.ThrowIfCancellationRequested();
        return output.ToArray();
    }

    private static byte[] DecompressBrotli(byte[] compressed, CancellationToken ct)
    {
        using var input = new MemoryStream(compressed);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        ct.ThrowIfCancellationRequested();
        return output.ToArray();
    }

    private static byte[] CompressGZip(byte[] raw, CancellationToken ct)
    {
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gz.Write(raw);
        ct.ThrowIfCancellationRequested();
        return output.ToArray();
    }

    private static byte[] DecompressGZip(byte[] compressed, CancellationToken ct)
    {
        using var input = new MemoryStream(compressed);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gz.CopyTo(output);
        ct.ThrowIfCancellationRequested();
        return output.ToArray();
    }
}
