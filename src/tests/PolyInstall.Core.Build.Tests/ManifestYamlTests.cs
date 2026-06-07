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
        m.Metadata.Name.Verify().ToBe("T");
        m.Build.Targets.Verify().ToHaveCount(1);
        m.Ui.LogoPath.Verify().ToBe("branding/logo.png");
        m.Ui.WizardSteps[0].Type.Verify().ToBe("welcome");
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

        m.Build.Signing.Verify().ToBeNull();
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

        m.Build.Signing.Verify().NotToBeNull();
        m.Build.Signing!.Windows.Verify().NotToBeNull();
        m.Build.Signing.Windows!.CertificatePath.Verify().ToBe("${WINDOWS_CERT_PATH}");
        m.Build.Signing.Windows.CertificatePasswordEnv.Verify().ToBe("WINDOWS_CERT_PASSWORD");
        m.Build.Signing.Windows.TimestampUrl.Verify().ToBe("http://timestamp.example.test");
        m.Build.Signing.Macos.Verify().NotToBeNull();
        m.Build.Signing.Macos!.Identity.Verify().ToBe("Developer ID Application: Example");
        m.Build.Signing.Macos.Keychain.Verify().ToBe("${MACOS_KEYCHAIN}");
        m.Build.Signing.Macos.NotarizationProfile.Verify().ToBe("polyinstall-notary");
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

        ((object?)m.FileAssociations).Verify().NotToBeNull();
        m.FileAssociations.Verify().ToHaveCount(1);
        m.FileAssociations![0].Extension.Verify().ToBe(".oef");
        m.FileAssociations[0].Description.Verify().ToBe("OEF File");
        m.FileAssociations[0].ProgId.Verify().ToBe("Custom.ProgId");
        m.FileAssociations[0].Icon.Verify().ToBe("app.ico");
        m.FileAssociations[0].Command.Verify().ToBe("\"my.exe\" \"%1\"");
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

        ((object?)m.FileAssociations).Verify().NotToBeNull();
        m.FileAssociations.Verify().ToHaveCount(1);
        m.FileAssociations![0].MimeType.Verify().ToBe("application/x-oef");
        m.FileAssociations[0].BundlePath.Verify().ToBe("/Applications/MyApp.app");
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

        ((Action)(() => ManifestYaml.Parse(yaml))).Throws<Exception>()
            .WithMessageContaining("typo_section");
    }
}
