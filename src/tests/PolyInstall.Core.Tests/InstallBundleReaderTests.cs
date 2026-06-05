using System.IO.Compression;
using System.Text;
using System.Text.Json;
using PolyInstall.Manifest;
using PolyInstall.Payload;

namespace PolyInstall.Core.Tests;

public class InstallBundleReaderTests
{
    [Fact]
    public void ReadFromStream_DecompressesPayloadZip()
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
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
