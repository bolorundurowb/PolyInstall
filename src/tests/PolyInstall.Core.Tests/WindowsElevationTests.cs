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
            isAdministrator: false).Must().BeTrue();
    }

    [Fact]
    public void ShouldRelaunchElevated_WhenAlreadyAdministrator_ReturnsFalse()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0", installScope: "machine");

        WindowsElevation.ShouldRelaunchElevated(
            manifest,
            existingInstall: null,
            isWindows: true,
            isAdministrator: true).Must().BeFalse();
    }

    [Fact]
    public void ShouldRelaunchElevated_WhenStateClaimsUnverifiableSystemService_ReturnsFalse()
    {
        if (OperatingSystem.IsWindows())
            return; // Ownership verification only runs on Windows; elsewhere it always fails closed.

        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0", installScope: "user");
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            var state = TestHelpers.StateFor(manifest, installRoot, "1.0.0");
            state.RegisteredServices =
            [
                new RegisteredServiceInfo
                {
                    Name = "Spooler",
                    Scope = "system",
                    Platform = "windows",
                },
            ];
            var existing = new ExistingInstallInfo
            {
                InstallLocation = installRoot,
                InstallScope = "user",
                State = state,
            };

            WindowsElevation.ShouldRelaunchElevated(
                manifest,
                existing,
                isWindows: true,
                isAdministrator: false).Must().BeFalse();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }
}
