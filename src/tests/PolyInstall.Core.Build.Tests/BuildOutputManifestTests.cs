using System.Text.Json;
using PolyInstall.Cli.Build;

namespace PolyInstall.Core.Build.Tests;

public class BuildOutputManifestTests
{
    [Fact]
    public void Serialize_UsesSnakeCase()
    {
        var manifest = new BuildOutputManifest(
            "SampleApp",
            "1.0.0",
            [
                new BuildArtifact("windows-x64", "win-x64", "installer", "/dist/SampleApp-windows-x64.exe", 12345678)
            ]);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        };
        var json = JsonSerializer.Serialize(manifest, options);

        json.Verify().ToContain(""""product_name": "SampleApp"""");
        json.Verify().ToContain(""""version": "1.0.0"""");
        json.Verify().ToContain(""""artifacts"""");
        json.Verify().ToContain(""""target": "windows-x64"""");
        json.Verify().ToContain(""""rid": "win-x64"""");
        json.Verify().ToContain(""""type": "installer"""");
        json.Verify().ToContain(""""path": "/dist/SampleApp-windows-x64.exe"""");
        json.Verify().ToContain(""""size": 12345678"""");
    }

    [Fact]
    public void Serialize_WithMultipleArtifacts_RendersAll()
    {
        var manifest = new BuildOutputManifest(
            "MyApp",
            "2.0.0",
            [
                new BuildArtifact("windows-x64", "win-x64", "installer", "C:\\dist\\MyApp-win-x64.exe", 100),
                new BuildArtifact("linux-x64", "linux-x64", "appimage", "/dist/MyApp-linux-x64.AppImage", 200),
                new BuildArtifact("osx-arm64", "osx-arm64", "dmg", "/dist/MyApp-osx-arm64.dmg", 300),
            ]);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
        };
        var json = JsonSerializer.Serialize(manifest, options);

        json.Verify().ToContain(""""type": "installer"""");
        json.Verify().ToContain(""""type": "appimage"""");
        json.Verify().ToContain(""""type": "dmg"""");
    }
}
