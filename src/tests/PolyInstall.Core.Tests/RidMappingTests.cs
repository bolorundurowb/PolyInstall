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
        RidMapping.ToDotNetRid(token).Verify().ToBe(expected);
    }

    [Theory]
    [InlineData("WINDOWS-X64")]
    [InlineData("  linux-x64  ")]
    public void ToDotNetRid_WithCasingOrWhitespace_IsCaseInsensitiveAndTrimmed(string token)
    {
        RidMapping.ToDotNetRid(token).Verify().ToBeOneOf("win-x64", "linux-x64");
    }

    [Fact]
    public void ToDotNetRid_WithUnknownToken_ThrowsArgumentException()
    {
        ((Action)(() => RidMapping.ToDotNetRid("unknown"))).Throws<ArgumentException>()
            .WithMessageContaining("Unknown build target RID token");
    }
}
