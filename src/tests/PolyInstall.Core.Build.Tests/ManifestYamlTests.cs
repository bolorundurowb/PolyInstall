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

    [Fact]
    public void Parse_WhenSigningOmitted_LeavesSigningNull()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            build:
              targets:
                - windows-x64
            files:
              - source_dir: .
                include:
                  - "*.txt"
            """;

        var m = ManifestYaml.Parse(yaml);

        m.Build.Signing.Should().BeNull();
    }

    [Fact]
    public void Parse_WithSigningOptions_RoundTrips()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            build:
              targets:
                - windows-x64
                - osx-arm64
              macos:
                package: dmg
              signing:
                windows:
                  certificate_path: "${WINDOWS_CERT_PATH}"
                  certificate_password_env: WINDOWS_CERT_PASSWORD
                  timestamp_url: "http://timestamp.example.test"
                macos:
                  identity: "Developer ID Application: Example"
                  keychain: "${MACOS_KEYCHAIN}"
                  notarization_profile: polyinstall-notary
            files:
              - source_dir: .
                include:
                  - "*.txt"
            """;

        var m = ManifestYaml.Parse(yaml);

        m.Build.Signing.Should().NotBeNull();
        m.Build.Signing!.Windows.Should().NotBeNull();
        m.Build.Signing.Windows!.CertificatePath.Should().Be("${WINDOWS_CERT_PATH}");
        m.Build.Signing.Windows.CertificatePasswordEnv.Should().Be("WINDOWS_CERT_PASSWORD");
        m.Build.Signing.Windows.TimestampUrl.Should().Be("http://timestamp.example.test");
        m.Build.Signing.Macos.Should().NotBeNull();
        m.Build.Signing.Macos!.Identity.Should().Be("Developer ID Application: Example");
        m.Build.Signing.Macos.Keychain.Should().Be("${MACOS_KEYCHAIN}");
        m.Build.Signing.Macos.NotarizationProfile.Should().Be("polyinstall-notary");
    }

    [Fact]
    public void Parse_WithFileAssociations_RoundTrips()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            file_associations:
              - extension: .oef
                description: OEF File
                prog_id: Custom.ProgId
                icon: app.ico
                command: '"my.exe" "%1"'
            files:
              - source_dir: .
            """;

        var m = ManifestYaml.Parse(yaml);

        m.FileAssociations.Should().NotBeNull();
        m.FileAssociations.Should().HaveCount(1);
        m.FileAssociations![0].Extension.Should().Be(".oef");
        m.FileAssociations[0].Description.Should().Be("OEF File");
        m.FileAssociations[0].ProgId.Should().Be("Custom.ProgId");
        m.FileAssociations[0].Icon.Should().Be("app.ico");
        m.FileAssociations[0].Command.Should().Be("\"my.exe\" \"%1\"");
    }

    [Fact]
    public void Parse_WithFileAssociationsPlatformFields_RoundTrips()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            file_associations:
              - extension: .oef
                description: OEF File
                command: open %1
                mime_type: application/x-oef
                bundle_path: /Applications/MyApp.app
            files:
              - source_dir: .
            """;

        var m = ManifestYaml.Parse(yaml);

        m.FileAssociations.Should().NotBeNull();
        m.FileAssociations.Should().HaveCount(1);
        m.FileAssociations![0].MimeType.Should().Be("application/x-oef");
        m.FileAssociations[0].BundlePath.Should().Be("/Applications/MyApp.app");
    }

    [Fact]
    public void Parse_WithUnknownProperty_Throws()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            typo_section:
              enabled: true
            files:
              - source_dir: .
            """;

        FluentActions.Invoking(() => ManifestYaml.Parse(yaml))
            .Should().Throw<Exception>()
            .WithMessage("*typo_section*");
    }
}
