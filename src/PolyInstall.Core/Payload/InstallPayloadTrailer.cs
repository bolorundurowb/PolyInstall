using System.Buffers.Binary;
using System.Text;

namespace PolyInstall.Core.Payload;

/// <summary>
/// Trailer appended after stub executable: <c>[original exe][manifest UTF-8][compressed payload][footer]</c>.
/// </summary>
public static class InstallPayloadTrailer
{
    public const int FooterSize = 20;
    public static ReadOnlySpan<byte> Magic => "POLYIN01"u8;

    public static void WriteFooter(Stream stream, int manifestLength, long payloadLength)
    {
        Span<byte> footer = stackalloc byte[FooterSize];
        BinaryPrimitives.WriteUInt64LittleEndian(footer[..8], (ulong)payloadLength);
        BinaryPrimitives.WriteUInt32LittleEndian(footer.Slice(8, 4), (uint)manifestLength);
        Magic.CopyTo(footer[^Magic.Length..]);
        stream.Write(footer);
    }

    /// <summary>
    /// Reads lengths from the last 20 bytes of <paramref name="stream"/> (must be seekable).
    /// </summary>
    public static (int ManifestLength, long PayloadLength) ReadFooter(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < FooterSize)
            throw new InvalidOperationException("Stream is too small or not seekable.");

        stream.Seek(-FooterSize, SeekOrigin.End);
        Span<byte> footer = stackalloc byte[FooterSize];
        var read = stream.Read(footer);
        if (read != FooterSize)
            throw new InvalidOperationException("Could not read payload footer.");

        if (!footer[^Magic.Length..].SequenceEqual(Magic))
            throw new InvalidOperationException("Payload trailer magic not found; not a PolyInstall bundle.");

        var payloadLen = BinaryPrimitives.ReadUInt64LittleEndian(footer.Slice(0, 8));
        var manifestLen = BinaryPrimitives.ReadUInt32LittleEndian(footer.Slice(8, 4));
        return ((int)manifestLen, (long)payloadLen);
    }

    /// <summary>
    /// Absolute stream offsets of manifest and payload blobs (after original stub bytes).
    /// </summary>
    public static (long ManifestStart, long PayloadStart) GetBlobOffsets(long fileLength, int manifestLength, long payloadLength)
    {
        var footerStart = fileLength - FooterSize;
        var payloadStart = footerStart - payloadLength;
        var manifestStart = payloadStart - manifestLength;
        if (manifestStart < 0)
            throw new InvalidOperationException("Invalid trailer lengths.");
        return (manifestStart, payloadStart);
    }

    public static string ReadManifestUtf8(Stream stream, long manifestStart, int manifestLength)
    {
        stream.Seek(manifestStart, SeekOrigin.Begin);
        var buffer = new byte[manifestLength];
        var readTotal = 0;
        while (readTotal < manifestLength)
        {
            var n = stream.Read(buffer, readTotal, manifestLength - readTotal);
            if (n == 0)
                throw new EndOfStreamException("Unexpected end of stream while reading manifest.");
            readTotal += n;
        }
        return Encoding.UTF8.GetString(buffer);
    }
}
