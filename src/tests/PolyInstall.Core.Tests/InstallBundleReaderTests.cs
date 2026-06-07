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
            bundle.Write(Encoding.UTF8.GetBytes("signature-like-bytes"));
            bundle.Position = 0;

            var (readManifest, readCompressed) = InstallBundleReader.ReadFromStream(bundle);
            readManifest.Metadata.Name.Verify().ToBe("App");
            readManifest.Build.Compression.Verify().ToBe("gzip");

            var zipBytes = InstallBundleReader.DecompressPayload(readManifest, readCompressed);
            using var zipMs = new MemoryStream(zipBytes);
            using var zip = new ZipArchive(zipMs, ZipArchiveMode.Read);
            var entry = zip.GetEntry("files/f.txt");
            entry.Verify().NotToBeNull();
            using var sr = new StreamReader(entry!.Open());
            sr.ReadToEnd().Verify().ToBe("payload-body");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public void DecompressPayloadToFile_WritesZipWithoutReadingPayloadBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "polyinstall-bundle-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        var sourcePath = Path.Combine(root, "source.txt");
        var bundlePath = Path.Combine(root, "installer.bin");
        var zipPath = Path.Combine(root, "payload.zip");
        File.WriteAllText(sourcePath, "payload-body");
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
                [("files/source.txt", sourcePath)],
                PayloadCompression.GZip);

            using (var bundle = File.Create(bundlePath))
            {
                bundle.Write(new byte[] { 0x4D, 0x5A });
                bundle.Write(manifestBytes);
                bundle.Write(compressed);
                InstallPayloadTrailer.WriteFooter(bundle, manifestBytes.Length, compressed.Length);
            }

            var readManifest = InstallBundleReader.ReadManifestFromSeekableFile(bundlePath);
            InstallBundleReader.DecompressPayloadToFile(bundlePath, readManifest, zipPath);

            using var zipFs = File.OpenRead(zipPath);
            using var zip = new ZipArchive(zipFs, ZipArchiveMode.Read);
            var entry = zip.GetEntry("files/source.txt");
            entry.Verify().NotToBeNull();
            using var sr = new StreamReader(entry!.Open());
            sr.ReadToEnd().Verify().ToBe("payload-body");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
