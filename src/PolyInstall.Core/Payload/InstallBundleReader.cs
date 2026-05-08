using System.Text.Json;
using PolyInstall.Core.Manifest;

namespace PolyInstall.Core.Payload;

public static class InstallBundleReader
{
    public static (InstallManifest Manifest, byte[] CompressedPayload) ReadFromSeekableFile(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        return ReadFromStream(fs);
    }

    public static (InstallManifest Manifest, byte[] CompressedPayload) ReadFromStream(Stream stream)
    {
        var (manifestLen, payloadLen) = InstallPayloadTrailer.ReadFooter(stream);
        var len = stream.Length;
        var (manifestStart, payloadStart) = InstallPayloadTrailer.GetBlobOffsets(len, manifestLen, payloadLen);
        var json = InstallPayloadTrailer.ReadManifestUtf8(stream, manifestStart, manifestLen);
        stream.Seek(payloadStart, SeekOrigin.Begin);
        if (payloadLen > Array.MaxLength)
            throw new NotSupportedException(
                $"Payload is too large to load into memory ({payloadLen:N0} bytes). Maximum supported size is {Array.MaxLength:N0} bytes.");
        var payload = new byte[payloadLen];
        stream.ReadExactly(payload);
        var manifest = JsonSerializer.Deserialize<InstallManifest>(json, InstallManifest.JsonOptions)
                       ?? throw new InvalidOperationException("Invalid embedded manifest JSON.");
        return (manifest, payload);
    }

    public static byte[] DecompressPayload(InstallManifest manifest, byte[] compressed)
    {
        var kind = PayloadArchive.ParseCompression(manifest.Build.Compression);
        return PayloadArchive.Decompress(compressed, kind);
    }
}
