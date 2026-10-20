using PolyInstall.Core.Build.Manifest;
using PolyInstall.Core.Build.Validation;

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
        m.Metadata.Name.Must().Be("T");
        m.Build.Targets.Must().HaveCount(1);
        m.Ui.LogoPath.Must().Be("branding/logo.png");
        m.Ui.WizardSteps[0].Type.Must().Be("welcome");
        m.Ui.Mode.Must().Be("wizard");
    }

    [Fact]
    public void Parse_ShouldSetSilentMode_WhenUiModeIsSilent()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            build:
              targets:
                - windows-x64
            ui:
              mode: silent
            files:
              - source_dir: .
                include:
                  - "*.txt"
            """;
        var m = ManifestYaml.Parse(yaml);
        m.Ui.Mode.Must().Be("silent");
        m.Ui.IsSilent.Must().BeTrue();
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

        m.Build.Signing.Must().BeNull();
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

        m.Build.Signing.Must().NotBeNull();
        m.Build.Signing!.Windows.Must().NotBeNull();
        m.Build.Signing.Windows!.CertificatePath.Must().Be("${WINDOWS_CERT_PATH}");
        m.Build.Signing.Windows.CertificatePasswordEnv.Must().Be("WINDOWS_CERT_PASSWORD");
        m.Build.Signing.Windows.TimestampUrl.Must().Be("http://timestamp.example.test");
        m.Build.Signing.Macos.Must().NotBeNull();
        m.Build.Signing.Macos!.Identity.Must().Be("Developer ID Application: Example");
        m.Build.Signing.Macos.Keychain.Must().Be("${MACOS_KEYCHAIN}");
        m.Build.Signing.Macos.NotarizationProfile.Must().Be("polyinstall-notary");
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

        ((object?)m.FileAssociations).Must().NotBeNull();
        m.FileAssociations.Must().HaveCount(1);
        m.FileAssociations![0].Extension.Must().Be(".oef");
        m.FileAssociations[0].Description.Must().Be("OEF File");
        m.FileAssociations[0].ProgId.Must().Be("Custom.ProgId");
        m.FileAssociations[0].Icon.Must().Be("app.ico");
        m.FileAssociations[0].Command.Must().Be("\"my.exe\" \"%1\"");
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

        ((object?)m.FileAssociations).Must().NotBeNull();
        m.FileAssociations.Must().HaveCount(1);
        m.FileAssociations![0].MimeType.Must().Be("application/x-oef");
        m.FileAssociations[0].BundlePath.Must().Be("/Applications/MyApp.app");
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

    [Fact]
    public void Parse_ProgressStep_ShowLogsDefaultsToTrue()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            ui:
              wizard_steps:
                - type: progress
                  title: Installing
            files:
              - source_dir: .
            """;

        var m = ManifestYaml.Parse(yaml);

        m.Ui.WizardSteps.Must().HaveCount(1);
        m.Ui.WizardSteps[0].Type.Must().Be("progress");
        m.Ui.WizardSteps[0].ShowLogs.Must().BeTrue();
    }

    [Fact]
    public void Parse_ProgressStep_ShowLogsFalse_IsPreserved()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            ui:
              wizard_steps:
                - type: progress
                  title: Installing
                  show_logs: false
            files:
              - source_dir: .
            """;

        var m = ManifestYaml.Parse(yaml);

        m.Ui.WizardSteps[0].ShowLogs.Must().BeFalse();
    }

    [Fact]
    public void Parse_ShouldReadServices_WhenServicesBlockIsPresent()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            build:
              targets:
                - linux-x64
            files:
              - source_dir: .
                include:
                  - "*.txt"
            services:
              - name: com.example.app
                require: os.isLinux
                scope: user
                description: "Background helper"
                executable: "{AppDir}/bin/app"
                arguments: ["--service"]
                working_directory: "{AppDir}"
                restart: on-failure
                start: true
                environment:
                  APP_HOME: "{AppDir}"
            """;

        var m = ManifestYaml.Parse(yaml);

        m.Services.Must().NotBeNull();
        m.Services.Must().HaveCount(1);
        var service = m.Services![0];
        service.Name.Must().Be("com.example.app");
        service.Require.Must().Be("os.isLinux");
        service.Scope.Must().Be("user");
        service.Executable.Must().Be("{AppDir}/bin/app");
        service.Arguments.Must().BeEquivalentTo(["--service"]);
        service.WorkingDirectory.Must().Be("{AppDir}");
        service.Restart.Must().Be("on-failure");
        service.Start.Must().BeTrue();
        service.Environment.Must().NotBeNull();
        service.Environment!["APP_HOME"].Must().Be("{AppDir}");
        ManifestSemanticValidator.Validate(m);
    }

    [Fact]
    public void Parse_ShouldKeepSetPermissionsMode_WhenModeIsAnUnquotedNumber()
    {
        var yaml = """
            metadata:
              name: T
              version: 1.0.0
            build:
              targets:
                - linux-x64
            files:
              - source_dir: .
                include:
                  - "*.txt"
            tasks:
              post_install:
                - require: os.isUnix
                  action: set_permissions
                  parameters:
                    path: "{AppDir}/app"
                    mode: 755
            """;

        var m = ManifestYaml.Parse(yaml);

        var mode = m.Tasks!.PostInstall![0].Parameters!["mode"];
        mode.Must().NotBeNull();
        mode!.ToString().Must().Be("755");
        ManifestSemanticValidator.Validate(m);
    }
}
