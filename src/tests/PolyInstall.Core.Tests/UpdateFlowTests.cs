using PolyInstall.Core.Install;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;

namespace PolyInstall.Core.Tests;

public class UpdateFlowTests
{
    [Fact]
    public void TryReadFromInstallDirectory_WithMatchingState_ReturnsExistingInstall()
    {
        var manifest = Manifest("SampleApp", "2.0.0");
        var installRoot = NewTempDir();
        try
        {
            InstallStateIo.WriteState(installRoot, StateFor(manifest, installRoot, "1.0.0"));

            var existing = InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot);

            existing.Should().NotBeNull();
            existing!.DisplayVersion.Should().Be("1.0.0");
            existing.InstallLocation.Should().Be(installRoot);
        }
        finally
        {
            TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void TryReadFromInstallDirectory_WithDifferentProduct_ReturnsNull()
    {
        var manifest = Manifest("SampleApp", "2.0.0");
        var otherManifest = Manifest("OtherApp", "1.0.0");
        var installRoot = NewTempDir();
        try
        {
            InstallStateIo.WriteState(installRoot, StateFor(otherManifest, installRoot, "1.0.0"));

            InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot).Should().BeNull();
        }
        finally
        {
            TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void TryReadFromInstallDirectory_WithCorruptState_ReturnsNull()
    {
        var manifest = Manifest("SampleApp", "2.0.0");
        var installRoot = NewTempDir();
        try
        {
            Directory.CreateDirectory(InstallStatePaths.PolyDir(installRoot));
            File.WriteAllText(InstallStatePaths.InstallStatePath(installRoot), "{not-json");

            InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot).Should().BeNull();
        }
        finally
        {
            TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void Find_WithExplicitCandidateDirectory_ReturnsMatchingInstall()
    {
        var manifest = Manifest("SampleApp", "2.0.0");
        var installRoot = NewTempDir();
        try
        {
            InstallStateIo.WriteState(installRoot, StateFor(manifest, installRoot, "1.0.0"));

            var existing = InstalledProductLocator.Find(manifest, new TestPal(installRoot), installRoot);

            existing.Should().NotBeNull();
            existing!.InstallLocation.Should().Be(installRoot);
        }
        finally
        {
            TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void InstallFinalizer_FinalizeInstall_WritesStateAndEmbeddedManifest_WhenArpDisabled()
    {
        var manifest = Manifest("SampleApp", "2.0.0");
        var installRoot = NewTempDir();
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
            TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void DeleteFilesMissingFromNewPayload_RemovesOnlyPreviouslyTrackedFiles()
    {
        var installRoot = NewTempDir();
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
            TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void DeleteFilesMissingFromNewPayload_SkipsGeneratedArtifactsAndPathEscapes()
    {
        var installRoot = NewTempDir();
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
            TryDeleteDirectory(installRoot);
            TryDeleteFile(parentFile);
        }
    }

    [Fact]
    public void InstallCoordinator_Run_WithExistingInventory_PrunesAndUpdatesState()
    {
        var manifest = Manifest("SampleApp", "2.0.0");
        var sourceRoot = NewTempDir();
        var installRoot = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "app.txt"), "new");
            File.WriteAllText(Path.Combine(installRoot, "app.txt"), "old");
            File.WriteAllText(Path.Combine(installRoot, "stale.txt"), "stale");
            File.WriteAllText(Path.Combine(installRoot, "user.txt"), "user");

            var previousState = StateFor(manifest, installRoot, "1.0.0", ["app.txt", "stale.txt"]);
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
            TryDeleteDirectory(sourceRoot);
            TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void InstallCoordinator_Run_WithLegacyExistingInstall_DoesNotPruneUnknownOldFiles()
    {
        var manifest = Manifest("SampleApp", "2.0.0");
        var sourceRoot = NewTempDir();
        var installRoot = NewTempDir();
        var progress = new List<string>();
        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "app.txt"), "new");
            File.WriteAllText(Path.Combine(installRoot, "stale.txt"), "legacy stale");
            InstallStateIo.WriteState(installRoot, StateFor(manifest, installRoot, "1.0.0"));
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
            TryDeleteDirectory(sourceRoot);
            TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void InstallCoordinator_Run_WithSameVersionExistingInstall_UsesRepairMode()
    {
        var manifest = Manifest("SampleApp", "2.0.0");
        var sourceRoot = NewTempDir();
        var installRoot = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "app.txt"), "repaired");
            InstallStateIo.WriteState(installRoot, StateFor(manifest, installRoot, "2.0.0", ["app.txt"]));
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
            TryDeleteDirectory(sourceRoot);
            TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void InstallCoordinator_Run_WithFreshDestination_UsesInstallModeAndCreatesState()
    {
        var manifest = Manifest("SampleApp", "2.0.0");
        var sourceRoot = NewTempDir();
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
            TryDeleteDirectory(sourceRoot);
            TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void WindowsElevation_ShouldRelaunchElevated_WhenExistingInstallIsMachineScope()
    {
        var manifest = Manifest("SampleApp", "2.0.0", installScope: "user");
        var existing = new ExistingInstallInfo { InstallScope = "machine" };

        WindowsElevation.ShouldRelaunchElevated(
            manifest,
            existing,
            isWindows: true,
            isAdministrator: false).Should().BeTrue();
    }

    [Fact]
    public void WindowsElevation_ShouldNotRelaunchElevated_WhenAlreadyAdministrator()
    {
        var manifest = Manifest("SampleApp", "2.0.0", installScope: "machine");

        WindowsElevation.ShouldRelaunchElevated(
            manifest,
            existingInstall: null,
            isWindows: true,
            isAdministrator: true).Should().BeFalse();
    }

    private static InstallManifest Manifest(string name, string version, string installScope = "user") =>
        new()
        {
            Metadata = new ManifestMetadata
            {
                Name = name,
                Version = version,
                Publisher = "Example",
            },
            Build = new BuildConfiguration
            {
                Windows = new WindowsBuildOptions
                {
                    InstallScope = installScope,
                    RegisterArp = false,
                },
            },
        };

    private static InstallStateDocument StateFor(
        InstallManifest manifest,
        string installRoot,
        string version,
        List<string>? payloadFiles = null)
    {
        var productId = ProductIdHelper.StableProductGuidString(manifest.Metadata);
        return new InstallStateDocument
        {
            ProductId = productId,
            DisplayName = manifest.Metadata.Name,
            DisplayVersion = version,
            Publisher = manifest.Metadata.Publisher,
            InstallLocation = installRoot,
            InstallScope = InstallScopeHelper.GetInstallScope(manifest),
            RegistryUninstallKeyRelative = WindowsArpRegistration.RegistryKeyRelativeForProductId(productId),
            PayloadFiles = payloadFiles,
        };
    }

    private static string NewTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "polyinstall-update-test-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }

    private sealed class TestPal(string appDir) : IPolyInstallPal
    {
        public string AppDir { get; } = appDir;
        public string ProgramFiles => Path.GetTempPath();
        public string UserHome => Path.GetTempPath();
        public string Desktop => Path.GetTempPath();
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
