using PolyInstall.Core.Build.Validation;

namespace PolyInstall.Core.Build.Tests;

public class YamlSourceIndexTests
{
    [Fact]
    public void Build_MapsDottedAndJsonPointerPathsToSpans()
    {
        var yaml = """
            metadata:
              name: Demo
              version: "1.0.0"
            files:
              - source_dir: app
                include:
                  - "**/*"
            """;

        var index = YamlSourceIndex.Build(yaml);

        index.TryGet("metadata.name", out var dotted).Must().BeTrue();
        dotted.Line.Must().BeGreaterThan(0);
        dotted.Column.Must().BeGreaterThan(0);

        index.TryGet("/metadata/name", out var pointer).Must().BeTrue();
        pointer.Must().Be(dotted);

        index.TryGet("files[0].source_dir", out var filePath).Must().BeTrue();
        filePath.Line.Must().BeGreaterThan(dotted.Line);
    }

    [Fact]
    public void Normalize_ConvertsDottedBracketPathsToJsonPointer()
    {
        YamlSourceIndex.Normalize("tasks.post_install[0].features")
            .Must().Be("/tasks/post_install/0/features");
        YamlSourceIndex.Normalize("/files/0/source_dir")
            .Must().Be("/files/0/source_dir");
    }

    [Fact]
    public void Prepare_AttachesSpansAndSortsByLocation()
    {
        var yaml = """
            metadata:
              name: ""
              version: ""
            files: []
            """;

        var diagnostics = new[]
        {
            new ManifestDiagnostic("PI106", "files must contain at least one entry.", "files"),
            new ManifestDiagnostic("PI101", "metadata.name must be non-empty.", "metadata.name"),
        };

        var prepared = ManifestDiagnosticPipeline.Prepare(diagnostics, yaml);

        prepared.Must().HaveCount(2);
        prepared[0].Code.Must().Be("PI101");
        prepared[0].Span.Must().NotBeNull();
        prepared[1].Code.Must().Be("PI106");
    }
}
