using PolyInstall.Install;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Tests;

public class DefaultInstallPathResolverTests
{
    [Fact]
    public void GetDefaultInstallPath_OnWindowsUserScope_UsesLocalAppData()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var pal = new TestPal(
            programFiles: @"C:\Program Files",
            localAppData: @"C:\Users\test\AppData\Local",
            userHome: @"C:\Users\test");
        var manifest = TestHelpers.Manifest("SampleApp", "1.0.0", installScope: "user");

        var path = DefaultInstallPathResolver.GetDefaultInstallPath(manifest, pal);

        path.Must().Be(Path.Combine(@"C:\Users\test\AppData\Local", "SampleApp"));
    }

    [Fact]
    public void GetDefaultInstallPath_OnWindowsMachineScope_UsesProgramFiles()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var pal = new TestPal(
            programFiles: @"C:\Program Files",
            localAppData: @"C:\Users\test\AppData\Local",
            userHome: @"C:\Users\test");
        var manifest = TestHelpers.Manifest("SampleApp", "1.0.0", installScope: "machine");

        var path = DefaultInstallPathResolver.GetDefaultInstallPath(manifest, pal);

        path.Must().Be(Path.Combine(@"C:\Program Files", "SampleApp"));
    }

    [Fact]
    public void GetDefaultInstallPath_OnNonWindows_UsesProgramFilesRegardlessOfScope()
    {
        if (OperatingSystem.IsWindows())
            return;

        var pal = new TestPal(
            programFiles: "/opt",
            localAppData: "/home/test/.local/share",
            userHome: "/home/test");
        var userManifest = TestHelpers.Manifest("SampleApp", "1.0.0", installScope: "user");
        var machineManifest = TestHelpers.Manifest("SampleApp", "1.0.0", installScope: "machine");

        DefaultInstallPathResolver.GetDefaultInstallPath(userManifest, pal)
            .Must().Be(Path.Combine("/opt", "SampleApp"));
        DefaultInstallPathResolver.GetDefaultInstallPath(machineManifest, pal)
            .Must().Be(Path.Combine("/opt", "SampleApp"));
    }

    private sealed class TestPal(string programFiles, string localAppData, string userHome) : IInstallPathPal
    {
        public string AppDir => string.Empty;
        public string ProgramFiles { get; } = programFiles;
        public string LocalAppData { get; } = localAppData;
        public string UserHome { get; } = userHome;
        public string Desktop => string.Empty;
    }
}
