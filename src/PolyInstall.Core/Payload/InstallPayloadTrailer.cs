using System.Buffers.Binary;
using System.Text;

namespace PolyInstall.Payload;

/// <summary>
/// Trailer appended after stub executable: <c>[original exe][manifest UTF-8][compressed payload][footer]</c>.
/// </summary>
public static class InstallPayloadTrailer
{
    public const int FooterSize = 20;
    private const int ScanChunkSize = 64 * 1024;
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
        var footer = ReadFooterWithOffset(stream);
        return (footer.ManifestLength, footer.PayloadLength);
    }

    /// <summary>
    /// Reads lengths and the absolute footer offset. If signing data was appended after the PolyInstall bundle,
    /// scans backward for the last valid payload footer.
    /// </summary>
    public static (int ManifestLength, long PayloadLength, long FooterStart) ReadFooterWithOffset(Stream stream)
    {
        if (!stream.CanSeek || stream.Length < FooterSize)
            throw new InvalidOperationException("Stream is too small or not seekable.");

        var endFooterStart = stream.Length - FooterSize;
        if (TryReadFooterAt(stream, endFooterStart, out var endFooter))
            return (endFooter.ManifestLength, endFooter.PayloadLength, endFooterStart);

        var magic = Magic.ToArray();
        var carry = Array.Empty<byte>();
        var endExclusive = stream.Length;
        while (endExclusive > 0)
        {
            var readLength = (int)Math.Min(ScanChunkSize, endExclusive);
            var chunkStart = endExclusive - readLength;
            var chunk = new byte[readLength + carry.Length];
            stream.Seek(chunkStart, SeekOrigin.Begin);
            stream.ReadExactly(chunk.AsSpan(0, readLength));
            carry.CopyTo(chunk.AsSpan(readLength));

            for (var i = chunk.Length - magic.Length; i >= 0; i--)
            {
                if (!chunk.AsSpan(i, magic.Length).SequenceEqual(magic))
                    continue;

                var magicStart = chunkStart + i;
                var footerStart = magicStart - (FooterSize - magic.Length);
                if (footerStart < 0 || footerStart + FooterSize > stream.Length)
                    continue;

                if (TryReadFooterAt(stream, footerStart, out var footer))
                    return (footer.ManifestLength, footer.PayloadLength, footerStart);
            }

            var carryLength = Math.Min(magic.Length - 1, readLength);
            carry = new byte[carryLength];
            Array.Copy(chunk, 0, carry, 0, carryLength);
            endExclusive = chunkStart;
        }

        throw new InvalidOperationException("Payload trailer magic not found; not a PolyInstall bundle.");
    }

    private static bool TryReadFooterAt(
        Stream stream,
        long footerStart,
        out (int ManifestLength, long PayloadLength) footer)
    {
        footer = default;
        if (footerStart < 0 || footerStart + FooterSize > stream.Length)
            return false;

        stream.Seek(footerStart, SeekOrigin.Begin);
        Span<byte> buffer = stackalloc byte[FooterSize];
        var read = stream.Read(buffer);
        if (read != FooterSize)
            return false;

        if (!buffer[^Magic.Length..].SequenceEqual(Magic))
            return false;

        var payloadLen = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(0, 8));
        var manifestLen = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(8, 4));
        if (manifestLen == 0 || payloadLen > long.MaxValue)
            return false;

        var payloadStart = footerStart - (long)payloadLen;
        var manifestStart = payloadStart - manifestLen;
        if (payloadStart < 0 || manifestStart < 0)
            return false;

        footer = ((int)manifestLen, (long)payloadLen);
        return true;
    }

    /// <summary>
    /// Absolute stream offsets of manifest and payload blobs (after original stub bytes).
    /// </summary>
    public static (long ManifestStart, long PayloadStart) GetBlobOffsets(long fileLength, int manifestLength, long payloadLength)
    {
        var footerStart = fileLength - FooterSize;
        return GetBlobOffsetsFromFooter(footerStart, manifestLength, payloadLength);
    }

    public static (long ManifestStart, long PayloadStart) GetBlobOffsetsFromFooter(
        long footerStart,
        int manifestLength,
        long payloadLength)
    {
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
