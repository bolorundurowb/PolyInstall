using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Payload;

namespace PolyInstall.Core.Tests;

public class PayloadBundleTests
{
    [Fact]
    public void InstallPayloadTrailer_WriteAndReadFooter_PreservesLengthsAndManifest()
    {
        using var ms = new MemoryStream();
        var stub = Encoding.UTF8.GetBytes("stub-exe-prefix");
        ms.Write(stub);
        var manifestJson = """{"metadata":{"name":"T","version":"1"},"build":{"output_dir":"o","compression":"gzip","targets":["linux-x64"]},"ui":{"theme":"dark","wizard_steps":[]},"files":[]}""";
        var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
        var innerZip = CreateMinimalZip("hello.txt", "hi");
        ms.Write(manifestBytes);
        ms.Write(innerZip);
        InstallPayloadTrailer.WriteFooter(ms, manifestBytes.Length, innerZip.Length);
        ms.Position = 0;

        var (manifestLen, payloadLen) = InstallPayloadTrailer.ReadFooter(ms);
        manifestLen.Should().Be(manifestBytes.Length);
        payloadLen.Should().Be(innerZip.Length);

        var (manifestStart, payloadStart) = InstallPayloadTrailer.GetBlobOffsets(ms.Length, manifestLen, payloadLen);
        manifestStart.Should().Be(stub.Length);
        payloadStart.Should().Be(stub.Length + manifestLen);

        var json = InstallPayloadTrailer.ReadManifestUtf8(ms, manifestStart, manifestLen);
        json.Should().Contain("\"name\":\"T\"");

        ms.Seek(payloadStart, SeekOrigin.Begin);
        var payload = new byte[payloadLen];
        ms.ReadExactly(payload, 0, (int)payloadLen);
        payload.Should().Equal(innerZip);
    }

    [Fact]
    public void InstallBundleReader_FromStream_DecompressesPayloadZip()
    {
        var root = Path.Combine(Path.GetTempPath(), "polyinstall-bundle-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        var filePath = Path.Combine(root, "f.txt");
        File.WriteAllText(filePath, "payload-body");
        try
        {
            var manifest = new InstallManifest
            {
                Metadata = new ManifestMetadata { Name = "App", Version = "1.0.0" },
                Build = new BuildConfiguration
                {
                    OutputDir = "out",
                    Compression = "gzip",
                    Targets = ["linux-x64"],
                },
                Ui = new UiConfiguration { Theme = "dark", WizardSteps = [] },
                Files = [],
            };
            var manifestJson = JsonSerializer.Serialize(manifest, InstallManifest.JsonOptions);
            var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
            var compressed = PayloadArchive.PackAndCompress(
                [("files/f.txt", filePath)],
                PayloadCompression.GZip);

            using var bundle = new MemoryStream();
            var prefix = new byte[] { 0x4D, 0x5A };
            bundle.Write(prefix);
            bundle.Write(manifestBytes);
            bundle.Write(compressed);
            InstallPayloadTrailer.WriteFooter(bundle, manifestBytes.Length, compressed.Length);
            bundle.Position = 0;

            var (readManifest, readCompressed) = InstallBundleReader.ReadFromStream(bundle);
            readManifest.Metadata.Name.Should().Be("App");
            readManifest.Build.Compression.Should().Be("gzip");

            var zipBytes = InstallBundleReader.DecompressPayload(readManifest, readCompressed);
            using var zipMs = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(zipMs, ZipArchiveMode.Read);
            var entry = zip.GetEntry("files/f.txt");
            entry.Should().NotBeNull();
            using var sr = new StreamReader(entry!.Open());
            sr.ReadToEnd().Should().Be("payload-body");
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public void InstallPayloadTrailer_GetBlobOffsets_WithInvalidLengths_ThrowsInvalidOperationException()
    {
        FluentActions.Invoking(() => InstallPayloadTrailer.GetBlobOffsets(10, 100, 100))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid trailer lengths*");
    }

    private static byte[] CreateMinimalZip(string entryName, string content)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var e = zip.CreateEntry(entryName);
            using var w = new StreamWriter(e.Open());
            w.Write(content);
        }

        return ms.ToArray();
    }
}
