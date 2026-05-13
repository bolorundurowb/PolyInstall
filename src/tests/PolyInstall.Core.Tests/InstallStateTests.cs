using PolyInstall.Core.Install;
using PolyInstall.Core.Manifest;

namespace PolyInstall.Core.Tests;

public class InstallStateTests
{
    [Fact]
    public void StableProductGuidString_WithIdenticalMetadata_ReturnsSameValue()
    {
        var m = new ManifestMetadata { Name = "App", Version = "1.0", Publisher = "Contoso" };
        var a = ProductIdHelper.StableProductGuidString(m);
        var b = ProductIdHelper.StableProductGuidString(m);
        a.Should().Be(b);
        a.Should().StartWith("{").And.EndWith("}");
    }

    [Fact]
    public void StableProductGuidString_WithGuidId_ReturnsUpperInvariantGuid()
    {
        var id = "{11111111-1111-1111-1111-111111111111}";
        var m = new ManifestMetadata { Id = id, Name = "X", Version = "1.0" };
        ProductIdHelper.StableProductGuidString(m).Should().Be(id.ToUpperInvariant());
    }

    [Fact]
    public void StableProductGuidString_WithNonGuidId_ReturnsDeterministicGuid()
    {
        var m = new ManifestMetadata { Id = "contoso-product-key", Name = "X", Version = "1.0" };
        var a = ProductIdHelper.StableProductGuidString(m);
        var b = ProductIdHelper.StableProductGuidString(m);
        a.Should().Be(b);
        Guid.TryParse(a.Trim('{', '}'), out _).Should().BeTrue();
    }

    [Fact]
    public void InstallStateIo_WriteState_ThenReadState_PreservesDocument()
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
}
