using PolyInstall.Cli.Build;
using PolyInstall.Manifest;
using Assert = Xunit.Assert;

namespace PolyInstall.Core.Build.Tests;

public class LogoRasterizerTests
{
    private const string SampleSvg =
        """<svg xmlns="http://www.w3.org/2000/svg" width="40" height="20" viewBox="0 0 40 20"><rect width="40" height="20" fill="#336699"/></svg>""";

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "polyinstall-logotest-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void DeleteTree(string path)
    {
        try { Directory.Delete(path, recursive: true); } catch { }
    }

    [Fact]
    public void RasterizeSvgLogo_WithSvgLogo_AddsPngEntryAndRewritesLogoPath()
    {
        var root = MakeTempDir();
        var temps = new List<string>();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "branding"));
            var svgPath = Path.Combine(root, "branding", "logo.svg");
            File.WriteAllText(svgPath, SampleSvg);
            var manifest = new InstallManifest { Ui = new UiConfiguration { LogoPath = "branding/logo.svg" } };
            var files = new List<(string EntryName, string FullPath)> { ("branding/logo.svg", svgPath) };
            var index = new PayloadFeatureIndex();

            InstallerBuildPipeline.RasterizeSvgLogo(manifest, root, files, index, temps);

            manifest.Ui.LogoPath.Must().Be("branding/logo.png");
            var png = files.Single(f => f.EntryName == "branding/logo.png");
            File.Exists(png.FullPath).Must().BeTrue();
            Assert.Equal(PngSignature, File.ReadAllBytes(png.FullPath).Take(PngSignature.Length).ToArray());
            index.CoreFiles.Must().Contain("branding/logo.png");
        }
        finally
        {
            foreach (var t in temps)
                DeleteTree(t);
            DeleteTree(root);
        }
    }

    [Fact]
    public void RasterizeSvgLogo_WithRasterLogo_LeavesManifestAndFilesUnchanged()
    {
        var manifest = new InstallManifest { Ui = new UiConfiguration { LogoPath = "branding/logo.png" } };
        var files = new List<(string EntryName, string FullPath)>();

        InstallerBuildPipeline.RasterizeSvgLogo(manifest, Path.GetTempPath(), files, null, []);

        manifest.Ui.LogoPath.Must().Be("branding/logo.png");
        Assert.Empty(files);
    }

    [Fact]
    public void RasterizeSvgLogo_WithInvalidSvg_ThrowsInsteadOfDroppingLogo()
    {
        var root = MakeTempDir();
        var temps = new List<string>();
        try
        {
            var svgPath = Path.Combine(root, "logo.svg");
            File.WriteAllText(svgPath, "not an svg");
            var manifest = new InstallManifest { Ui = new UiConfiguration { LogoPath = "logo.svg" } };
            var files = new List<(string EntryName, string FullPath)> { ("logo.svg", svgPath) };

            Assert.Throws<InvalidOperationException>(
                () => InstallerBuildPipeline.RasterizeSvgLogo(manifest, root, files, null, temps));
        }
        finally
        {
            foreach (var t in temps)
                DeleteTree(t);
            DeleteTree(root);
        }
    }
}
