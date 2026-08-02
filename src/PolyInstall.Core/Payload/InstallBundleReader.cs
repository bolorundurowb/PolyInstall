using System.IO.Compression;
using PolyInstall.Manifest;
using InstallJsonContext = PolyInstall.Manifest.InstallJsonContext;

namespace PolyInstall.Payload;

public static class InstallBundleReader
{
    public static (InstallManifest Manifest, byte[] CompressedPayload) ReadFromSeekableFile(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return ReadFromStream(fs);
    }

    public static (InstallManifest Manifest, byte[] CompressedPayload) ReadFromStream(Stream stream)
    {
        var (manifestLen, payloadLen, footerStart) = InstallPayloadTrailer.ReadFooterWithOffset(stream);
        var (manifestStart, payloadStart) = InstallPayloadTrailer.GetBlobOffsetsFromFooter(footerStart, manifestLen, payloadLen);
        var json = InstallPayloadTrailer.ReadManifestUtf8(stream, manifestStart, manifestLen);
        stream.Seek(payloadStart, SeekOrigin.Begin);
        if (payloadLen > Array.MaxLength)
            throw new NotSupportedException(
                $"Payload is too large to load into memory ({payloadLen:N0} bytes). Maximum supported size is {Array.MaxLength:N0} bytes.");
        var payload = new byte[payloadLen];
        stream.ReadExactly(payload);
        var manifest = System.Text.Json.JsonSerializer.Deserialize(json, InstallJsonContext.Default.InstallManifest)
                       ?? throw new InvalidOperationException("Invalid embedded manifest JSON.");
        RuntimeManifestGuard.Validate(manifest);
        return (manifest, payload);
    }

    public static InstallManifest ReadManifestFromSeekableFile(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return ReadManifestFromStream(fs);
    }

    public static InstallManifest ReadManifestFromStream(Stream stream)
    {
        var (manifestLen, payloadLen, footerStart) = InstallPayloadTrailer.ReadFooterWithOffset(stream);
        var (manifestStart, _) = InstallPayloadTrailer.GetBlobOffsetsFromFooter(footerStart, manifestLen, payloadLen);
        var json = InstallPayloadTrailer.ReadManifestUtf8(stream, manifestStart, manifestLen);
        var manifest = System.Text.Json.JsonSerializer.Deserialize(json, InstallJsonContext.Default.InstallManifest)
                       ?? throw new InvalidOperationException("Invalid embedded manifest JSON.");
        RuntimeManifestGuard.Validate(manifest);
        return manifest;
    }

    public static byte[] DecompressPayload(InstallManifest manifest, byte[] compressed)
    {
        var kind = PayloadArchive.ParseCompression(manifest.Build.Compression);
        return PayloadArchive.Decompress(compressed, kind);
    }

    public static void DecompressPayloadToFile(
        string bundlePath,
        InstallManifest manifest,
        string outputZipPath,
        CancellationToken ct = default)
    {
        using var fs = File.OpenRead(bundlePath);
        var (manifestLen, payloadLen, footerStart) = InstallPayloadTrailer.ReadFooterWithOffset(fs);
        var (_, payloadStart) = InstallPayloadTrailer.GetBlobOffsetsFromFooter(footerStart, manifestLen, payloadLen);
        fs.Seek(payloadStart, SeekOrigin.Begin);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputZipPath))!);
        using var limitedPayload = new LimitedReadStream(fs, payloadLen);
        using var decompressed = CreateDecompressionStream(limitedPayload, PayloadArchive.ParseCompression(manifest.Build.Compression));
        using var output = File.Create(outputZipPath);
        InstallPayloadLimits.CopyWithLimit(decompressed, output, InstallPayloadLimits.MaxDecompressedPayloadBytes, ct);
    }

    private static Stream CreateDecompressionStream(Stream input, PayloadCompression compression)
    {
        return compression switch
        {
            PayloadCompression.Brotli => new BrotliStream(input, CompressionMode.Decompress, leaveOpen: false),
            PayloadCompression.GZip => new GZipStream(input, CompressionMode.Decompress, leaveOpen: false),
            _ => throw new ArgumentOutOfRangeException(nameof(compression)),
        };
    }

    private sealed class LimitedReadStream(Stream inner, long length) : Stream
    {
        private long _remaining = length;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position
        {
            get => length - _remaining;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remaining <= 0)
                return 0;

            var toRead = (int)Math.Min(count, _remaining);
            var read = inner.Read(buffer, offset, toRead);
            _remaining -= read;
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            if (_remaining <= 0)
                return 0;

            var toRead = (int)Math.Min(buffer.Length, _remaining);
            var read = inner.Read(buffer[..toRead]);
            _remaining -= read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
