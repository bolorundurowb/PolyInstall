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
        var payload = new byte[payloadLen];
        var read = stream.Read(payload, 0, (int)payloadLen);
        if (read != payloadLen)
            throw new EndOfStreamException("Could not read full payload.");
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
