using PolyInstall.Core.Install;

namespace PolyInstall.Core.Tests;

public class PayloadFileInventoryTests
{
    [Fact]
    public void Enumerate_WithFlatFiles_ReturnsRelativePaths()
    {
        var root = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, "a.txt"), "a");
            File.WriteAllText(Path.Combine(root, "b.txt"), "b");

            var files = PayloadFileInventory.Enumerate(root);

            files.Should().BeEquivalentTo("a.txt", "b.txt");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void Enumerate_WithNestedFiles_ReturnsForwardSlashRelativePaths()
    {
        var root = TestHelpers.NewTempDir();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            File.WriteAllText(Path.Combine(root, "sub", "c.txt"), "c");

            var files = PayloadFileInventory.Enumerate(root);

            files.Should().ContainSingle().Which.Should().Be("sub/c.txt");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void Enumerate_WithEmptyDirectory_ReturnsEmptyList()
    {
        var root = TestHelpers.NewTempDir();
        try
        {
            PayloadFileInventory.Enumerate(root).Should().BeEmpty();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(root);
        }
    }

    [Fact]
    public void Enumerate_WithNonexistentDirectory_ReturnsEmptyList()
    {
        PayloadFileInventory.Enumerate(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid().ToString("n")))
            .Should().BeEmpty();
    }

    [Fact]
    public void Enumerate_ResultsAreSortedCaseInsensitive()
    {
        var root = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(root, "Z.txt"), "");
            File.WriteAllText(Path.Combine(root, "a.txt"), "");
            File.WriteAllText(Path.Combine(root, "M.txt"), "");

            var files = PayloadFileInventory.Enumerate(root);

            files.Should().Equal("a.txt", "M.txt", "Z.txt");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(root);
        }
    }

    [Theory]
    [InlineData(@"dir\file.txt", "dir/file.txt")]
    [InlineData("/leading/slash.txt", "leading/slash.txt")]
    [InlineData("mixed\\dir/file.txt", "mixed/dir/file.txt")]
    public void NormalizeRelativePath_NormalizesSeparatorsAndTrimsLeadingSlash(string input, string expected)
    {
        PayloadFileInventory.NormalizeRelativePath(input).Should().Be(expected);
    }
}
