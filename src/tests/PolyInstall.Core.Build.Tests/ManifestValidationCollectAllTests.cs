using PolyInstall.Cli.Validation;
using PolyInstall.Core.Build.Validation;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Build.Tests;

public class ManifestValidationCollectAllTests
{
    [Fact]
    public void ValidateResult_WithEmptyJson_ReturnsStructuredDiagnostic()
    {
        var result = ManifestJsonValidator.ValidateResult(" ", FindSchemaPath());

        result.IsValid.Must().BeFalse();
        result.Diagnostics.Must().HaveCount(1);
        result.Diagnostics[0].Code.Must().Be("PI001");
        result.Diagnostics[0].Message.Must().Contain("empty");
    }

    [Fact]
    public void ValidateResult_WithMultipleSchemaViolations_IncludesInstancePathsAndHelp()
    {
        var json = """
            {
              "metadata": { "name": 123, "version": "1.0.0" },
              "build": { "targets": ["windows-x64"] },
              "files": [ { "source_dir": ".", "include": ["*.txt"] } ],
              "typo_section": true
            }
            """;

        var result = ManifestJsonValidator.ValidateResult(json, FindSchemaPath());

        result.IsValid.Must().BeFalse();
        result.Diagnostics.Must().HaveCountGreaterThan(1);
        result.Diagnostics.Select(d => d.Path).Must().Contain("/metadata/name");
        result.Diagnostics.Select(d => d.Path).Must().Contain("/typo_section");
        result.Diagnostics.Any(d => d.Code == "PI004").Must().BeTrue();
        result.Diagnostics.Any(d => d.Code == "PI005").Must().BeTrue();
        result.Diagnostics.Any(d =>
                d.Help is not null
                && d.Help.Contains("unknown property", StringComparison.OrdinalIgnoreCase))
            .Must().BeTrue();
        result.Diagnostics.Any(d =>
                d.Message.Contains("expected string", StringComparison.OrdinalIgnoreCase))
            .Must().BeTrue();
    }

    [Fact]
    public void ValidateResult_Semantic_ReturnsStructuredCodesAndPaths()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Metadata.Name = "";
        manifest.Metadata.Version = "";
        manifest.Files = [];

        var result = ManifestSemanticValidator.ValidateResult(manifest);

        result.IsValid.Must().BeFalse();
        result.Diagnostics.Must().HaveCountGreaterThan(1);
        result.Diagnostics.Any(d => d.Code == "PI101").Must().BeTrue();
        result.Diagnostics.Any(d => d.Code == "PI102").Must().BeTrue();
        result.Diagnostics.Any(d => d.Code == "PI106").Must().BeTrue();
        result.Diagnostics.Any(d => d.Path == "metadata.name").Must().BeTrue();
        result.Diagnostics.Any(d => d.Help is not null).Must().BeTrue();
    }

    [Fact]
    public void CollectAll_SchemaAndSemanticFailures_AreBothReportedWithSpans()
    {
        var yaml = """
            metadata:
              name: ""
              version: 1.0.0
            build:
              targets:
                - windows-x64
            files: []
            typo_section: true
            """;

        // Schema path uses JSON from the object model; semantic uses the typed manifest.
        var json = """
            {
              "metadata": { "name": "", "version": "1.0.0" },
              "build": { "targets": ["windows-x64"] },
              "files": [],
              "typo_section": true
            }
            """;
        var schema = ManifestJsonValidator.ValidateResult(json, FindSchemaPath());

        var manifest = CreateBaseManifest("user");
        manifest.Metadata.Name = "";
        manifest.Files = [];
        var semantic = ManifestSemanticValidator.ValidateResult(manifest);

        var prepared = ManifestDiagnosticPipeline.Prepare(
            schema.Diagnostics.Concat(semantic.Diagnostics),
            yaml);

        schema.IsValid.Must().BeFalse();
        semantic.IsValid.Must().BeFalse();
        prepared.Any(d => d.Code is "PI001" or "PI005").Must().BeTrue();
        prepared.Any(d => d.Code.StartsWith("PI1", StringComparison.Ordinal)).Must().BeTrue();
        prepared.Any(d => d.Span is not null).Must().BeTrue();

        var formatted = DiagnosticFormatter.Format(prepared, "polyinstall.yaml", yaml);
        formatted.Must().Contain("error[");
        formatted.Must().Contain("manifest validation failed with");
        formatted.Must().Contain("^");
    }

    private static string FindSchemaPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "schema", "v1.json");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate schema/v1.json for tests.");
    }

    [Fact]
    public void ValidateResult_UserScopeWithHklmRegistry_EmitsStableCodeAndHelp()
    {
        var manifest = CreateBaseManifest("user");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "write_registry",
                    Require = "os.isWindows",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["key_path"] = @"HKLM\Software\MyApp",
                        ["value_name"] = "",
                        ["value"] = "test",
                        ["value_kind"] = "string",
                    },
                },
            ],
        };

        var result = ManifestSemanticValidator.ValidateResult(manifest);

        result.IsValid.Must().BeFalse();
        var diagnostic = result.Diagnostics.Single(d => d.Code == "PI171");
        diagnostic.Message.Must().Contain("HKLM");
        diagnostic.Message.Must().Contain("install_scope is 'user'");
        diagnostic.Path.Must().Contain("tasks.post_install");
        diagnostic.Help.Must().Contain("HKCU");
    }

    private static InstallManifest CreateBaseManifest(string installScope) =>
        new()
        {
            Metadata = new ManifestMetadata { Name = "Test", Version = "1.0.0" },
            Build = new BuildConfiguration
            {
                Targets = ["windows-x64"],
                Windows = new WindowsBuildOptions { InstallScope = installScope },
            },
            Ui = new UiConfiguration { WizardSteps = [] },
            Files = [new FilesEntry { SourceDir = ".", Include = ["*.txt"] }],
        };
}
