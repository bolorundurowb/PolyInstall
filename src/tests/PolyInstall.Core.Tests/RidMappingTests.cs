using PolyInstall.Build;

namespace PolyInstall.Core.Tests;

public class RidMappingTests
{
    [Theory]
    [InlineData("windows-x64", "win-x64")]
    [InlineData("windows-arm64", "win-arm64")]
    [InlineData("linux-x64", "linux-x64")]
    [InlineData("linux-arm64", "linux-arm64")]
    [InlineData("osx-x64", "osx-x64")]
    [InlineData("osx-arm64", "osx-arm64")]
    public void ToDotNetRid_WithKnownToken_ReturnsExpectedRid(string token, string expected)
    {
        RidMapping.ToDotNetRid(token).Should().Be(expected);
    }

    [Theory]
    [InlineData("WINDOWS-X64")]
    [InlineData("  linux-x64  ")]
    public void ToDotNetRid_WithCasingOrWhitespace_IsCaseInsensitiveAndTrimmed(string token)
    {
        RidMapping.ToDotNetRid(token).Should().BeOneOf("win-x64", "linux-x64");
    }

    [Fact]
    public void ToDotNetRid_WithUnknownToken_ThrowsArgumentException()
    {
        FluentActions.Invoking(() => RidMapping.ToDotNetRid("unknown"))
            .Should().Throw<ArgumentException>()
            .WithMessage("*Unknown build target RID token*");
    }
}
