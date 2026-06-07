using System.Text.Json;
using PolyInstall.Install;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Tests;

public class InstallStateIoTests
{
    [Fact]
    public void WriteState_WithSelectedFeatures_RoundTrips()
    {
        var state = new InstallStateDocument
        {
            ProductId = "{AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE}",
            DisplayName = "Test",
            DisplayVersion = "2.0",
            InstallLocation = @"C:\Apps\Test",
            InstallScope = "user",
            RegistryUninstallKeyRelative = "key",
            SelectedFeatures = ["samples", "simulator"],
        };
        var installRoot = Path.Combine(Path.GetTempPath(), "polyinstall-state-features-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(installRoot);
        try
        {
            InstallStateIo.WriteState(installRoot, state);
            var json = File.ReadAllText(InstallStatePaths.InstallStatePath(installRoot));
            json.Verify().ToContain("selected_features");

            var read = InstallStateIo.ReadState(installRoot);
            read.SelectedFeatures.Verify().ToBeEquivalentTo(new[] { "samples", "simulator" });
        }
        finally
        {
            try { Directory.Delete(installRoot, true); } catch { }
        }
    }

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
            File.Exists(path).Verify().ToBeTrue();
            var json = File.ReadAllText(path);
            json.Verify().ToContain("registry_uninstall_key_relative");

            var read = InstallStateIo.ReadState(installRoot);
            read.DisplayName.Verify().ToBe("Test");
            read.InstallScope.Verify().ToBe("user");
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

            read.Metadata.Name.Verify().ToBe("Test");
            read.Tasks.Verify().NotToBeNull();
            var postInstall = read.Tasks!.PostInstall!;
            postInstall.Verify().ToHaveCount(1);
            var task = postInstall[0];
            task.Action.Verify().ToBe("create_shortcut");
            task.Parameters.Verify().NotToBeNull();
            task.Parameters!.Verify().ContainKey("target_path");
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

            json.Verify().ToContain("\"action\": \"create_shortcut\"");
            json.Verify().ToContain("\"target_path\"");
            json.Verify().ToContain("\"name\"");
            json.Verify().ToContain("\"location\"");
        }
        finally
        {
            try { Directory.Delete(installRoot, true); } catch { }
        }
    }
}
