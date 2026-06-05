using System.IO.Compression;
using System.Text;
using PolyInstall.Payload;

namespace PolyInstall.Core.Tests;

public class InstallPayloadTrailerTests
{
    [Fact]
    public void WriteAndReadFooter_PreservesLengthsAndManifest()
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
    public void ReadFooter_WhenSignatureBytesFollowFooter_FindsBundleFooter()
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
        var footerStart = ms.Length - InstallPayloadTrailer.FooterSize;
        ms.Write(Encoding.UTF8.GetBytes("signature-bytes-after-footer"));
        ms.Position = 0;

        var (manifestLen, payloadLen, actualFooterStart) = InstallPayloadTrailer.ReadFooterWithOffset(ms);

        manifestLen.Should().Be(manifestBytes.Length);
        payloadLen.Should().Be(innerZip.Length);
        actualFooterStart.Should().Be(footerStart);
        var (manifestStart, payloadStart) =
            InstallPayloadTrailer.GetBlobOffsetsFromFooter(actualFooterStart, manifestLen, payloadLen);
        manifestStart.Should().Be(stub.Length);
        payloadStart.Should().Be(stub.Length + manifestLen);
    }

    [Fact]
    public void GetBlobOffsets_WithInvalidLengths_ThrowsInvalidOperationException()
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
