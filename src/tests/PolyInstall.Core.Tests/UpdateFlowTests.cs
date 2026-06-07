using PolyInstall.Install;
using PolyInstall.Pal;

namespace PolyInstall.Core.Tests;

public class UpdateFlowTests
{
    [Fact]
    public void TryReadFromInstallDirectory_WithMatchingState_ReturnsExistingInstall()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            InstallStateIo.WriteState(installRoot, TestHelpers.StateFor(manifest, installRoot, "1.0.0"));

            var existing = InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot);

            existing.Should().NotBeNull();
            existing!.DisplayVersion.Should().Be("1.0.0");
            existing.InstallLocation.Should().Be(installRoot);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void TryReadFromInstallDirectory_WithDifferentProduct_ReturnsNull()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var otherManifest = TestHelpers.Manifest("OtherApp", "1.0.0");
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            InstallStateIo.WriteState(installRoot, TestHelpers.StateFor(otherManifest, installRoot, "1.0.0"));

            InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot).Should().BeNull();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void TryReadFromInstallDirectory_WithCorruptState_ReturnsNull()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            Directory.CreateDirectory(InstallStatePaths.PolyDir(installRoot));
            File.WriteAllText(InstallStatePaths.InstallStatePath(installRoot), "{not-json");

            InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot).Should().BeNull();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void Find_WithExplicitCandidateDirectory_ReturnsMatchingInstall()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            InstallStateIo.WriteState(installRoot, TestHelpers.StateFor(manifest, installRoot, "1.0.0"));

            var existing = InstalledProductLocator.Find(manifest, new TestPal(installRoot), installRoot);

            existing.Should().NotBeNull();
            existing!.InstallLocation.Should().Be(installRoot);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void InstallFinalizer_FinalizeInstall_WritesStateAndEmbeddedManifest_WhenArpDisabled()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            var state = InstallFinalizer.FinalizeInstall(manifest, installRoot, ["app.txt"]);

            File.Exists(InstallStatePaths.InstallStatePath(installRoot)).Should().BeTrue();
            File.Exists(InstallStatePaths.EmbeddedManifestPath(installRoot)).Should().BeTrue();
            state.DisplayVersion.Should().Be("2.0.0");
            state.PayloadFiles.Should().BeEquivalentTo("app.txt");
            InstallStateIo.ReadEmbeddedManifest(installRoot).Metadata.Name.Should().Be("SampleApp");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void DeleteFilesMissingFromNewPayload_RemovesOnlyPreviouslyTrackedFiles()
    {
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(installRoot, "stale.txt"), "old");
            File.WriteAllText(Path.Combine(installRoot, "keep.txt"), "old keep");
            File.WriteAllText(Path.Combine(installRoot, "user.txt"), "user");
            var newPayloadFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "keep.txt" };

            PayloadFileInventory.DeleteFilesMissingFromNewPayload(
                installRoot,
                ["stale.txt", "keep.txt"],
                newPayloadFiles);

            File.Exists(Path.Combine(installRoot, "stale.txt")).Should().BeFalse();
            File.Exists(Path.Combine(installRoot, "keep.txt")).Should().BeTrue();
            File.Exists(Path.Combine(installRoot, "user.txt")).Should().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void DeleteFilesMissingFromNewPayload_SkipsGeneratedArtifactsAndPathEscapes()
    {
        var installRoot = TestHelpers.NewTempDir();
        var parentFile = Path.Combine(Directory.GetParent(installRoot)!.FullName, "outside-" + Guid.NewGuid().ToString("n") + ".txt");
        try
        {
            Directory.CreateDirectory(InstallStatePaths.PolyDir(installRoot));
            File.WriteAllText(InstallStatePaths.InstallStatePath(installRoot), "state");
            File.WriteAllText(InstallStatePaths.EmbeddedManifestPath(installRoot), "manifest");
            File.WriteAllText(Path.Combine(installRoot, InstallStatePaths.UninstallExeFileName), "uninstall");
            File.WriteAllText(parentFile, "outside");

            PayloadFileInventory.DeleteFilesMissingFromNewPayload(
                installRoot,
                [
                    $"{InstallStatePaths.PolyDirName}/{InstallStatePaths.InstallStateFileName}",
                    $"{InstallStatePaths.PolyDirName}/{InstallStatePaths.EmbeddedManifestFileName}",
                    InstallStatePaths.UninstallExeFileName,
                    Path.GetRelativePath(installRoot, parentFile),
                ],
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            File.Exists(InstallStatePaths.InstallStatePath(installRoot)).Should().BeTrue();
            File.Exists(InstallStatePaths.EmbeddedManifestPath(installRoot)).Should().BeTrue();
            File.Exists(Path.Combine(installRoot, InstallStatePaths.UninstallExeFileName)).Should().BeTrue();
            File.Exists(parentFile).Should().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
            TestHelpers.TryDeleteFile(parentFile);
        }
    }

    [Fact]
    public void InstallCoordinator_Run_WithExistingInventory_PrunesAndUpdatesState()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var sourceRoot = TestHelpers.NewTempDir();
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "app.txt"), "new");
            File.WriteAllText(Path.Combine(installRoot, "app.txt"), "old");
            File.WriteAllText(Path.Combine(installRoot, "stale.txt"), "stale");
            File.WriteAllText(Path.Combine(installRoot, "user.txt"), "user");

            var previousState = TestHelpers.StateFor(manifest, installRoot, "1.0.0", ["app.txt", "stale.txt"]);
            InstallStateIo.WriteState(installRoot, previousState);
            var existing = InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot);

            var result = InstallCoordinator.Run(new InstallOperationOptions
            {
                Manifest = manifest,
                ExtractRoot = sourceRoot,
                Destination = installRoot,
                Pal = new TestPal(installRoot),
                ExistingInstall = existing,
            });

            result.Mode.Should().Be(InstallMode.Update);
            File.ReadAllText(Path.Combine(installRoot, "app.txt")).Should().Be("new");
            File.Exists(Path.Combine(installRoot, "stale.txt")).Should().BeFalse();
            File.Exists(Path.Combine(installRoot, "user.txt")).Should().BeTrue();

            var updatedState = InstallStateIo.ReadState(installRoot);
            updatedState.DisplayVersion.Should().Be("2.0.0");
            updatedState.PayloadFiles.Should().BeEquivalentTo("app.txt");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(sourceRoot);
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void InstallCoordinator_Run_WithLegacyExistingInstall_DoesNotPruneUnknownOldFiles()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var sourceRoot = TestHelpers.NewTempDir();
        var installRoot = TestHelpers.NewTempDir();
        var progress = new List<string>();
        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "app.txt"), "new");
            File.WriteAllText(Path.Combine(installRoot, "stale.txt"), "legacy stale");
            InstallStateIo.WriteState(installRoot, TestHelpers.StateFor(manifest, installRoot, "1.0.0"));
            var existing = InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot);

            var result = InstallCoordinator.Run(new InstallOperationOptions
            {
                Manifest = manifest,
                ExtractRoot = sourceRoot,
                Destination = installRoot,
                Pal = new TestPal(installRoot),
                ExistingInstall = existing,
                Progress = progress.Add,
            });

            result.Mode.Should().Be(InstallMode.Update);
            File.Exists(Path.Combine(installRoot, "stale.txt")).Should().BeTrue();
            progress.Should().Contain("No previous file inventory found; obsolete payload cleanup skipped");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(sourceRoot);
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void InstallCoordinator_Run_WithSameVersionExistingInstall_UsesRepairMode()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var sourceRoot = TestHelpers.NewTempDir();
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "app.txt"), "repaired");
            InstallStateIo.WriteState(installRoot, TestHelpers.StateFor(manifest, installRoot, "2.0.0", ["app.txt"]));
            var existing = InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot);

            var result = InstallCoordinator.Run(new InstallOperationOptions
            {
                Manifest = manifest,
                ExtractRoot = sourceRoot,
                Destination = installRoot,
                Pal = new TestPal(installRoot),
                ExistingInstall = existing,
            });

            result.Mode.Should().Be(InstallMode.Repair);
            File.ReadAllText(Path.Combine(installRoot, "app.txt")).Should().Be("repaired");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(sourceRoot);
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void InstallCoordinator_Run_WithFreshDestination_UsesInstallModeAndCreatesState()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var sourceRoot = TestHelpers.NewTempDir();
        var installRoot = Path.Combine(Path.GetTempPath(), "polyinstall-update-test-" + Guid.NewGuid().ToString("n"));
        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "app.txt"), "fresh");

            var result = InstallCoordinator.Run(new InstallOperationOptions
            {
                Manifest = manifest,
                ExtractRoot = sourceRoot,
                Destination = installRoot,
                Pal = new TestPal(installRoot),
            });

            result.Mode.Should().Be(InstallMode.Install);
            result.CreatedInstallDirectory.Should().BeTrue();
            File.ReadAllText(Path.Combine(installRoot, "app.txt")).Should().Be("fresh");
            InstallStateIo.ReadState(installRoot).PayloadFiles.Should().BeEquivalentTo("app.txt");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(sourceRoot);
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void UninstallCoordinator_Run_WhenStateLocationDoesNotMatchRequestedRoot_ThrowsBeforeDelete()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var stateRoot = TestHelpers.NewTempDir();
        var requestedRoot = TestHelpers.NewTempDir();
        try
        {
            var state = TestHelpers.StateFor(manifest, stateRoot, "2.0.0");

            var act = () => UninstallCoordinator.Run(
                state,
                manifest,
                new TestPal(stateRoot),
                Path.Combine(requestedRoot, InstallStatePaths.UninstallExeFileName),
                requestedRoot);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*does not match the requested install directory*");
            Directory.Exists(stateRoot).Should().BeTrue();
            Directory.Exists(requestedRoot).Should().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(stateRoot);
            TestHelpers.TryDeleteDirectory(requestedRoot);
        }
    }

    [Fact]
    public void UninstallCoordinator_Run_WhenStateProductDoesNotMatchManifest_ThrowsBeforeDelete()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var otherManifest = TestHelpers.Manifest("OtherApp", "2.0.0");
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            var state = TestHelpers.StateFor(otherManifest, installRoot, "2.0.0");

            var act = () => UninstallCoordinator.Run(
                state,
                manifest,
                new TestPal(installRoot),
                Path.Combine(installRoot, InstallStatePaths.UninstallExeFileName),
                installRoot);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*product id does not match*");
            Directory.Exists(installRoot).Should().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void UninstallCoordinator_Run_WhenInstallRootIsVolumeRoot_ThrowsBeforeDelete()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var volumeRoot = Path.GetPathRoot(Path.GetTempPath());
        if (string.IsNullOrWhiteSpace(volumeRoot))
            return;

        var installRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(volumeRoot));
        var state = TestHelpers.StateFor(manifest, installRoot, "2.0.0");

        var act = () => UninstallCoordinator.Run(
            state,
            manifest,
            new TestPal(installRoot),
            Path.Combine(installRoot, InstallStatePaths.UninstallExeFileName),
            installRoot);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*unsafe install root*");
    }

    private sealed class TestPal(string appDir) : IPolyInstallPal
    {
        public string AppDir { get; } = appDir;
        public string ProgramFiles => System.IO.Path.GetTempPath();
        public string UserHome => System.IO.Path.GetTempPath();
        public string Desktop => System.IO.Path.GetTempPath();
        public IShortcutPal Shortcuts { get; } = new NoOpShortcutPal();
        public IRegistryPal? Registry => null;
        public IDesktopEntryPal? DesktopEntries => null;
        public IFilePermissionsPal? FilePermissions => null;
        public IPathPal? Path => null;
        public IFileAssociationPal? FileAssociations => null;
        public IServiceManagerPal? Services => null;
    }

    private sealed class NoOpShortcutPal : IShortcutPal
    {
        public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath)
        {
        }
    }
}
