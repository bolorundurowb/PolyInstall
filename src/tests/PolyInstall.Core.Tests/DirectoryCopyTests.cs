using PolyInstall.Core.Install;

namespace PolyInstall.Core.Tests;

public class DirectoryCopyTests
{
    [Fact]
    public void CopyRecursive_WithProgressCallback_ReportsRelativeFilePaths()
    {
        var source = Path.Combine(Path.GetTempPath(), "polyinstall-copy-source-" + Guid.NewGuid().ToString("n"));
        var dest = Path.Combine(Path.GetTempPath(), "polyinstall-copy-dest-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "root.txt"), "root");
        File.WriteAllText(Path.Combine(source, "nested", "child.txt"), "child");
        var reported = new List<string>();

        try
        {
            DirectoryCopy.CopyRecursive(source, dest, onFileCopying: reported.Add);

            File.Exists(Path.Combine(dest, "root.txt")).Should().BeTrue();
            File.Exists(Path.Combine(dest, "nested", "child.txt")).Should().BeTrue();
            reported.Should().BeEquivalentTo("root.txt", Path.Combine("nested", "child.txt"));
        }
        finally
        {
            TryDeleteDirectory(source);
            TryDeleteDirectory(dest);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
