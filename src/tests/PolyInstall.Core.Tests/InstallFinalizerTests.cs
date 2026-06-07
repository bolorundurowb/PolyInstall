using PolyInstall.Install;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Tests;

public class InstallFinalizerTests
{
    [Fact]
    public void CreateState_PopulatesAllFieldsFromManifest()
    {
        var manifest = new InstallManifest
        {
            Metadata = new ManifestMetadata
            {
                Name = "MyApp",
                Version = "1.2.3",
                Publisher = "Contoso",
            },
            Build = new BuildConfiguration
            {
                Windows = new WindowsBuildOptions { InstallScope = "user" },
            },
        };
        var installDir = @"C:\Apps\MyApp";
        var payloadFiles = new[] { "app.exe", "config.json" };

        var state = InstallFinalizer.CreateState(manifest, installDir, payloadFiles);

        state.DisplayName.Verify().ToBe("MyApp");
        state.DisplayVersion.Verify().ToBe("1.2.3");
        state.Publisher.Verify().ToBe("Contoso");
        state.InstallLocation.Verify().ToBe(installDir);
        state.InstallScope.Verify().ToBe("user");
        state.PayloadFiles.Verify().ToBeEquivalentTo(new[] { "app.exe", "config.json" });
        state.ProductId.Verify().NotToBeNullOrWhiteSpace();
        state.RegistryUninstallKeyRelative.Verify().NotToBeNullOrWhiteSpace();
    }

    [Fact]
    public void CreateState_WithMachineScope_SetsScopeToMachine()
    {
        var manifest = new InstallManifest
        {
            Metadata = new ManifestMetadata { Name = "App", Version = "1.0" },
            Build = new BuildConfiguration
            {
                Windows = new WindowsBuildOptions { InstallScope = "machine" },
            },
        };

        var state = InstallFinalizer.CreateState(manifest, @"C:\App", []);

        state.InstallScope.Verify().ToBe("machine");
    }

    [Fact]
    public void CreateState_PayloadFilesAreSortedCaseInsensitive()
    {
        var manifest = new InstallManifest
        {
            Metadata = new ManifestMetadata { Name = "App", Version = "1.0" },
        };
        var files = new[] { "Z.exe", "a.exe", "M.exe" };

        var state = InstallFinalizer.CreateState(manifest, "C:\\App", files);

        state.PayloadFiles!.SequenceEqual(new[] { "a.exe", "M.exe", "Z.exe" }).Verify().ToBeTrue();
    }
}
