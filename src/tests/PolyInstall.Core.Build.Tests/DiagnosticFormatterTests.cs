using PolyInstall.Cli.Validation;
using PolyInstall.Core.Build.Validation;

namespace PolyInstall.Core.Build.Tests;

public class DiagnosticFormatterTests
{
    [Fact]
    public void Format_WithPathAndHelp_EmitsRustcStyleBlocksAndSummary()
    {
        var text = DiagnosticFormatter.Format(
            [
                new ManifestDiagnostic(
                    "PI001",
                    "required property 'name' is missing",
                    "/metadata",
                    "Add the required property at this location."),
                new ManifestDiagnostic(
                    "PI171",
                    "write_registry uses HKLM, but install_scope is 'user'",
                    "tasks.post_install[0]",
                    "Use HKCU, or set build.windows.install_scope to 'machine'."),
            ],
            "polyinstall.yaml");

        text.Must().Contain("error[PI001]: required property 'name' is missing");
        text.Must().Contain("--> polyinstall.yaml");
        text.Must().Contain("= note: at /metadata");
        text.Must().Contain("= help: Add the required property at this location.");
        text.Must().Contain("error[PI171]: write_registry uses HKLM, but install_scope is 'user'");
        text.Must().Contain("= note: at tasks.post_install[0]");
        text.Must().Contain("error: manifest validation failed with 2 errors");
    }

    [Fact]
    public void Format_WithSingleErrorWithoutPath_OmitsNoteAndUsesSingularSummary()
    {
        var text = DiagnosticFormatter.Format(
            [new ManifestDiagnostic("PI001", "Manifest JSON is empty.")],
            "manifest.yaml");

        text.Must().Contain("error[PI001]: Manifest JSON is empty.");
        text.Must().Contain("--> manifest.yaml");
        text.Must().NotContain("= note:");
        text.Must().Contain("error: manifest validation failed with 1 error");
    }

    [Fact]
    public void Format_WithYamlSpan_EmitsCaretUnderline()
    {
        var yaml = """
            metadata:
              name: Demo
              version: 1.0.0
            """;

        var text = DiagnosticFormatter.Format(
            [
                new ManifestDiagnostic(
                    "PI101",
                    "metadata.name must be non-empty.",
                    "metadata.name",
                    "Set metadata.name to your product name."),
            ],
            "polyinstall.yaml",
            yaml);

        text.Must().Contain("--> polyinstall.yaml:");
        text.Must().Contain("|");
        text.Must().Contain("^");
        text.Must().Contain("= help: Set metadata.name to your product name.");
        text.Must().NotContain("= note: at");
    }
}
