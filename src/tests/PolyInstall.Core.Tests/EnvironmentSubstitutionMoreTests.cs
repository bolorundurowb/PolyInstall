using System.Collections.ObjectModel;
using PolyInstall.Core.Manifest;

namespace PolyInstall.Core.Tests;

public class EnvironmentSubstitutionMoreTests
{
    [Fact]
    public void Substitute_KeepsPlaceholder_WhenUnsetAndNoDefault()
    {
        const string name = "POLYINSTALL_TEST_UNSET_VAR_9F3A";
        Environment.SetEnvironmentVariable(name, null);
        var input = $"pre${{{name}}}post";
        EnvironmentSubstitution.Substitute(input, ReadOnlyDictionary<string, string>.Empty)
            .Should().Be(input);
    }

    [Fact]
    public void Substitute_UsesExtraDictionary_OverEnvironment()
    {
        const string name = "POLYINSTALL_TEST_EXTRA_9F3A";
        Environment.SetEnvironmentVariable(name, "from-env");
        try
        {
            var extra = new Dictionary<string, string> { [name] = "from-extra" };
            EnvironmentSubstitution.Substitute($"${{{name}}}", extra).Should().Be("from-extra");
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Fact]
    public void ApplyToManifest_ReplacesNestedStrings()
    {
        var manifest = new InstallManifest
        {
            Metadata = new ManifestMetadata { Name = "X", Version = "1" },
            Build = new BuildConfiguration { OutputDir = "${OUT_DIR:-out}" },
        };
        var extra = new Dictionary<string, string> { ["OUT_DIR"] = "dist" };
        var applied = EnvironmentSubstitution.ApplyToManifest(manifest, extra);
        applied.Build.OutputDir.Should().Be("dist");
    }
}
