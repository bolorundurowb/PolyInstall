using PolyInstall.Install;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Tests;

public class FeatureFilterTests
{
    [Fact]
    public void ComputeAllowedFiles_WithNullIndex_AllowsAllFiles()
    {
        var allowed = FeatureFilter.ComputeAllowedFiles(
            index: null,
            payloadFiles: ["a.txt", "b/c.txt"],
            selectedFeatures: new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        allowed.Must().BeEquivalentTo(["a.txt", "b/c.txt"]);
    }

    [Fact]
    public void ComputeAllowedFiles_AllowsCoreFilesAlways()
    {
        var index = new PayloadFeatureIndex
        {
            CoreFiles = ["core.txt"],
            FeatureFiles =
            {
                ["sim"] = ["sim.txt"],
            },
        };

        var allowed = FeatureFilter.ComputeAllowedFiles(
            index,
            ["core.txt", "sim.txt"],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        allowed.Must().Contain("core.txt");
        allowed.Must().NotToContain("sim.txt");
    }

    [Fact]
    public void ComputeAllowedFiles_AllowsFeatureFilesWhenSelected()
    {
        var index = new PayloadFeatureIndex
        {
            CoreFiles = ["core.txt"],
            FeatureFiles =
            {
                ["sim"] = ["sim.txt"],
                ["samples"] = ["samples/s.txt"],
            },
        };

        var allowed = FeatureFilter.ComputeAllowedFiles(
            index,
            ["core.txt", "sim.txt", "samples/s.txt"],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sim" });

        allowed.Must().BeEquivalentTo(["core.txt", "sim.txt"]);
    }

    [Fact]
    public void ComputeAllowedFiles_FilesNotInIndex_TreatedAsCore()
    {
        var index = new PayloadFeatureIndex
        {
            FeatureFiles =
            {
                ["sim"] = ["sim.txt"],
            },
        };

        var allowed = FeatureFilter.ComputeAllowedFiles(
            index,
            ["sim.txt", ".polyinstall/tools/PolyInstall.Uninstall.exe"],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        allowed.Must().Contain(".polyinstall/tools/PolyInstall.Uninstall.exe");
        allowed.Must().NotToContain("sim.txt");
    }

    [Fact]
    public void ComputeAllowedFiles_FileInMultipleFeatures_AllowedWhenAnySelected()
    {
        var index = new PayloadFeatureIndex
        {
            FeatureFiles =
            {
                ["a"] = ["shared.txt"],
                ["b"] = ["shared.txt"],
            },
        };

        var allowed = FeatureFilter.ComputeAllowedFiles(
            index,
            ["shared.txt"],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "b" });

        allowed.Must().Contain("shared.txt");
    }

    [Fact]
    public void IsActive_EmptyOrNullList_AlwaysActive()
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "x" };
        FeatureFilter.IsActive(null, selected).Must().BeTrue();
        FeatureFilter.IsActive([], selected).Must().BeTrue();
    }

    [Fact]
    public void IsActive_WithIntersection_ReturnsTrue()
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "x", "y" };
        FeatureFilter.IsActive(["y"], selected).Must().BeTrue();
    }

    [Fact]
    public void IsActive_WithoutIntersection_ReturnsFalse()
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "x" };
        FeatureFilter.IsActive(["y"], selected).Must().BeFalse();
    }
}
