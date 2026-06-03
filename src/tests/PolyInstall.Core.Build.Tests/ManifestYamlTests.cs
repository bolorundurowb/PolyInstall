using PolyInstall.Core.Build.Manifest;

namespace PolyInstall.Core.Build.Tests;

public class ManifestYamlTests
{
    [Fact]
    public void Parse_WithMinimalDoc_RoundTrips()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            build:
              output_dir: out
              compression: brotli
              targets:
                - windows-x64
            ui:
              theme: light
              logo_path: branding/logo.png
              wizard_steps:
                - type: welcome
                  title: Hi
            files:
              - source_dir: .
                include:
                  - "*.txt"
            """;
        var m = ManifestYaml.Parse(yaml);
        m.Metadata.Name.Should().Be("T");
        m.Build.Targets.Should().ContainSingle();
        m.Ui.LogoPath.Should().Be("branding/logo.png");
        m.Ui.WizardSteps[0].Type.Should().Be("welcome");
    }
}
