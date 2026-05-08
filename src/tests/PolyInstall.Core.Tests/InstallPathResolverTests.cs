using PolyInstall.Core.Hosting;
using PolyInstall.Core.Install;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;

namespace PolyInstall.Core.Tests;

public class InstallPathResolverTests
{
    [Theory]
    [InlineData(TargetOperatingSystem.Windows, @"C:\Users\bolorundurowb", @"C:\Users\bolorundurowb\SampleApp")]
    [InlineData(TargetOperatingSystem.Linux, "/home/bolorundurowb", "/home/bolorundurowb/SampleApp")]
    [InlineData(TargetOperatingSystem.MacOs, "/Users/bolorundurowb", "/Users/bolorundurowb/SampleApp")]
    public void Expand_NormalizesForwardSlashes_ForTargetOs(
        TargetOperatingSystem targetOs,
        string userHome,
        string expected)
    {
        var pal = new TestInstallPathPal(userHome);

        var result = InstallPathResolver.Expand("{UserHome}/SampleApp", pal, targetOs);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(TargetOperatingSystem.Windows, @"C:\Users\bolorundurowb", @"C:\Users\bolorundurowb\SampleApp")]
    [InlineData(TargetOperatingSystem.Linux, "/home/bolorundurowb", "/home/bolorundurowb/SampleApp")]
    [InlineData(TargetOperatingSystem.MacOs, "/Users/bolorundurowb", "/Users/bolorundurowb/SampleApp")]
    public void Expand_NormalizesBackwardSlashes_ForTargetOs(
        TargetOperatingSystem targetOs,
        string userHome,
        string expected)
    {
        var pal = new TestInstallPathPal(userHome);

        var result = InstallPathResolver.Expand(@"{UserHome}\SampleApp", pal, targetOs);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("win-x64", true, TargetOperatingSystem.Windows)]
    [InlineData("windows-arm64", true, TargetOperatingSystem.Windows)]
    [InlineData("linux-x64", true, TargetOperatingSystem.Linux)]
    [InlineData("osx-arm64", true, TargetOperatingSystem.MacOs)]
    [InlineData("macos-14", true, TargetOperatingSystem.MacOs)]
    [InlineData(null, false, TargetOperatingSystem.Windows)]
    [InlineData("", false, TargetOperatingSystem.Windows)]
    [InlineData("   ", false, TargetOperatingSystem.Windows)]
    [InlineData("freebsd-x64", false, TargetOperatingSystem.Windows)]
    public void TryParseInstallerTargetOperatingSystem_MapsKnownRidPrefixes(
        string? token,
        bool expectedOk,
        TargetOperatingSystem expectedOs)
    {
        var ok = InstallPathResolver.TryParseInstallerTargetOperatingSystem(token, out var os);
        ok.Should().Be(expectedOk);
        if (expectedOk)
            os.Should().Be(expectedOs);
    }

    [Fact]
    public void Expand_UsesInstallerTargetToken_FromManifest()
    {
        var pal = new TestPolyInstallPal(@"C:\Users\bolorundurowb");
        var manifest = new InstallManifest
        {
            Build = new BuildConfiguration
            {
                InstallerTarget = "linux-x64",
            },
        };
        InstallBootstrap.Init(manifest, Path.GetTempPath(), pal);

        var result = InstallPathResolver.Expand("{UserHome}\\SampleApp", pal);

        result.Should().Be("C:/Users/bolorundurowb/SampleApp");
    }

    private sealed class TestInstallPathPal(string userHome) : IInstallPathPal
    {
        public string AppDir => string.Empty;
        public string ProgramFiles => string.Empty;
        public string UserHome { get; } = userHome;
        public string Desktop => string.Empty;
    }

    private sealed class TestPolyInstallPal(string userHome) : IPolyInstallPal
    {
        public string AppDir => string.Empty;
        public string ProgramFiles => string.Empty;
        public string UserHome { get; } = userHome;
        public string Desktop => string.Empty;
        public IShortcutPal Shortcuts { get; } = new NoOpShortcutPal();
        public IRegistryPal? Registry => null;
        public IDesktopEntryPal? DesktopEntries => null;
        public IFilePermissionsPal? FilePermissions => null;
    }

    private sealed class NoOpShortcutPal : IShortcutPal
    {
        public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath)
        {
        }
    }
}
