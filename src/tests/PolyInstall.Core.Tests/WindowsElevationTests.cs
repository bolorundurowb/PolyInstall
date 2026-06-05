using PolyInstall.Install;

namespace PolyInstall.Core.Tests;

public class WindowsElevationTests
{
    [Fact]
    public void ShouldRelaunchElevated_WhenExistingInstallIsMachineScope_ReturnsTrue()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0", installScope: "user");
        var existing = new ExistingInstallInfo { InstallScope = "machine" };

        WindowsElevation.ShouldRelaunchElevated(
            manifest,
            existing,
            isWindows: true,
            isAdministrator: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldRelaunchElevated_WhenAlreadyAdministrator_ReturnsFalse()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0", installScope: "machine");

        WindowsElevation.ShouldRelaunchElevated(
            manifest,
            existingInstall: null,
            isWindows: true,
            isAdministrator: true).Should().BeFalse();
    }
}
