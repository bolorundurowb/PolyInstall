using System.Text.Json;
using PolyInstall.Install;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Tests;

public class InstallStateIoTests
{
    [Fact]
    public void WriteState_ThenReadState_PreservesDocument()
    {
        var state = new InstallStateDocument
        {
            ProductId = "{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}",
            DisplayName = "Test",
            DisplayVersion = "2.0",
            Publisher = "Pub",
            InstallLocation = @"C:\Apps\Test",
            InstallScope = "user",
            RegistryUninstallKeyRelative = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}",
        };
        var installRoot = Path.Combine(Path.GetTempPath(), "polyinstall-state-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(installRoot);
        try
        {
            InstallStateIo.WriteState(installRoot, state);
            var path = InstallStatePaths.InstallStatePath(installRoot);
            File.Exists(path).Should().BeTrue();
            var json = File.ReadAllText(path);
            json.Should().Contain("registry_uninstall_key_relative");

            var read = InstallStateIo.ReadState(installRoot);
            read.DisplayName.Should().Be("Test");
            read.InstallScope.Should().Be("user");
        }
        finally
        {
            try { Directory.Delete(installRoot, true); } catch { }
        }
    }

    [Fact]
    public void WriteEmbeddedManifest_WithJsonElementParameters_RoundTrips()
    {
        var installRoot = Path.Combine(Path.GetTempPath(), "polyinstall-manifest-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(installRoot);
        try
        {
            var manifest = new InstallManifest
            {
                Metadata = new ManifestMetadata { Name = "Test", Version = "1.0.0" },
                Build = new BuildConfiguration { Targets = ["windows-x64"] },
                Tasks = new TasksConfiguration
                {
                    PostInstall =
                    [
                        new InstallTask
                        {
                            Action = "create_shortcut",
                            Parameters = new Dictionary<string, object?>
                            {
                                ["target_path"] = "app.exe",
                                ["name"] = "app",
                                ["location"] = "desktop",
                            },
                        },
                    ],
                },
            };

            InstallStateIo.WriteEmbeddedManifest(installRoot, manifest);
            var read = InstallStateIo.ReadEmbeddedManifest(installRoot);

            read.Metadata.Name.Should().Be("Test");
            read.Tasks.Should().NotBeNull();
            read.Tasks!.PostInstall.Should().ContainSingle();
            var task = read.Tasks.PostInstall[0];
            task.Action.Should().Be("create_shortcut");
            task.Parameters.Should().NotBeNull();
            task.Parameters!.Should().ContainKey("target_path");
        }
        finally
        {
            try { Directory.Delete(installRoot, true); } catch { }
        }
    }

    [Fact]
    public void WriteEmbeddedManifest_WithJsonElementParameters_SerializesCorrectly()
    {
        var installRoot = Path.Combine(Path.GetTempPath(), "polyinstall-manifest-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(installRoot);
        try
        {
            var manifest = new InstallManifest
            {
                Metadata = new ManifestMetadata { Name = "Test", Version = "1.0.0" },
                Build = new BuildConfiguration { Targets = ["windows-x64"] },
                Tasks = new TasksConfiguration
                {
                    PostInstall =
                    [
                        new InstallTask
                        {
                            Action = "create_shortcut",
                            Parameters = new Dictionary<string, object?>
                            {
                                ["target_path"] = "app.exe",
                                ["name"] = "app",
                                ["location"] = "desktop",
                            },
                        },
                    ],
                },
            };

            InstallStateIo.WriteEmbeddedManifest(installRoot, manifest);
            var path = InstallStatePaths.EmbeddedManifestPath(installRoot);
            var json = File.ReadAllText(path);

            json.Should().Contain("\"action\": \"create_shortcut\"");
            json.Should().Contain("\"target_path\"");
            json.Should().Contain("\"name\"");
            json.Should().Contain("\"location\"");
        }
        finally
        {
            try { Directory.Delete(installRoot, true); } catch { }
        }
    }
}
