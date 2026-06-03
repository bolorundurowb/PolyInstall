using PolyInstall.Core.Payload;

namespace PolyInstall.Core.Tests;

public class PayloadArchiveTests
{
    [Theory]
    [InlineData("lzma")]
    [InlineData("xz")]
    [InlineData("deflate")]
    public void ParseCompression_WithUnsupportedName_ThrowsArgumentException(string name)
    {
        FluentActions.Invoking(() => PayloadArchive.ParseCompression(name))
            .Should().Throw<ArgumentException>()
            .WithMessage("*brotli*gzip*");
    }

    [Theory]
    [InlineData("brotli")]
    [InlineData("gzip")]
    [InlineData("Brotli")]
    public void ParseCompression_WithSupportedName_DoesNotThrow(string name)
    {
        FluentActions.Invoking(() => PayloadArchive.ParseCompression(name)).Should().NotThrow();
    }
}
