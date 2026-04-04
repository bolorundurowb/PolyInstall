using PolyInstall.Core.Conditions;
using PolyInstall.Core.Globbing;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Payload;

namespace PolyInstall.Core.Tests;

public class CoreManifestTests
{
    [Fact]
    public void EnvironmentSubstitution_Replaces_VarAndDefault()
    {
        var json = """{"a":"x${MISSING:-d}y","b":{"c":"${FOO}"}}""";
        Environment.SetEnvironmentVariable("FOO", "bar");
        try
        {
            var s = EnvironmentSubstitution.ApplyToJson(json);
            s.Should().Contain("\"a\": \"xdy\"");
            s.Should().Contain("\"c\": \"bar\"");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FOO", null);
        }
    }

    [Fact]
    public void ManifestYaml_RoundTrip_Minimal()
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
        m.Ui.WizardSteps[0].Type.Should().Be("welcome");
    }

    [Fact]
    public void GlobResolver_FindsFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "polyinstall-glob-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(Path.Combine(root, "sub"));
        File.WriteAllText(Path.Combine(root, "a.txt"), "a");
        File.WriteAllText(Path.Combine(root, "sub", "b.txt"), "b");
        var files = GlobResolver.Collect(root, ".", ["**/*.txt"], null);
        try
        {
            files.Should().HaveCount(2);
            files.Select(f => f.RelativePath).Should().BeEquivalentTo("a.txt", "sub/b.txt");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* ignore */ }
        }
    }

    [Theory]
    [InlineData("os.isWindows")]
    [InlineData("os.isLinux")]
    public void ConditionEvaluator_KnownOsPredicates_DoNotThrow(string expr)
    {
        FluentActions.Invoking(() => ConditionEvaluator.Evaluate(expr)).Should().NotThrow();
    }

    [Theory]
    [InlineData("lzma")]
    [InlineData("xz")]
    [InlineData("deflate")]
    public void ParseCompression_RejectsUnsupportedAlgorithms(string name)
    {
        FluentActions.Invoking(() => PayloadArchive.ParseCompression(name))
            .Should().Throw<ArgumentException>()
            .WithMessage("*brotli*gzip*");
    }

    [Theory]
    [InlineData("brotli")]
    [InlineData("gzip")]
    [InlineData("Brotli")]
    public void ParseCompression_AcceptsBrotliAndGzip(string name)
    {
        FluentActions.Invoking(() => PayloadArchive.ParseCompression(name)).Should().NotThrow();
    }
}
