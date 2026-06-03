using PolyInstall.Install;

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
}
