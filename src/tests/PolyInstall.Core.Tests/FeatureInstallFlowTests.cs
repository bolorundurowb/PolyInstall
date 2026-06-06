using PolyInstall.Hosting;
using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Core.Tests;

[Collection("Sequential")]
public class FeatureInstallFlowTests : IDisposable
{
    private readonly InstallManifest? _priorManifest;
    private readonly string? _priorInstallDirectory;
    private readonly HashSet<string> _priorSelectedFeatures;
    private readonly ExistingInstallInfo? _priorExistingInstall;
    private readonly InstallMode _priorMode;

    public FeatureInstallFlowTests()
    {
        // Snapshot static InstallBootstrap state so we can restore after each test
        // and avoid bleeding into PalImplementationTests / TaskEngineTests.
        _priorManifest = SafeGet(() => InstallBootstrap.Manifest);
        _priorInstallDirectory = InstallBootstrap.InstallDirectory;
        _priorSelectedFeatures = new HashSet<string>(InstallBootstrap.SelectedFeatures, StringComparer.OrdinalIgnoreCase);
        _priorExistingInstall = InstallBootstrap.ExistingInstall;
        _priorMode = InstallBootstrap.SelectedInstallMode;
    }

    public void Dispose()
    {
        InstallBootstrap.InstallDirectory = _priorInstallDirectory;
        InstallBootstrap.ExistingInstall = _priorExistingInstall;
        InstallBootstrap.SelectedInstallMode = _priorMode;
        InstallBootstrap.SelectedFeatures = _priorSelectedFeatures;
    }

    private static T? SafeGet<T>(Func<T> getter) where T : class
    {
        try { return getter(); } catch { return null; }
    }

    [Fact]
    public void InstallCoordinator_Run_CopiesOnlySelectedFeatureFiles_AndPersistsSelection()
    {
        var manifest = BuildManifestWithFeatures();
        var sourceRoot = TestHelpers.NewTempDir();
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            // payload layout: core + feat-a + feat-b
            File.WriteAllText(Path.Combine(sourceRoot, "core.txt"), "core");
            Directory.CreateDirectory(Path.Combine(sourceRoot, "a"));
            File.WriteAllText(Path.Combine(sourceRoot, "a", "a.txt"), "a");
            Directory.CreateDirectory(Path.Combine(sourceRoot, "b"));
            File.WriteAllText(Path.Combine(sourceRoot, "b", "b.txt"), "feat-b");

            InstallBootstrap.Init(manifest, sourceRoot, new TestPal(installRoot));
            InstallBootstrap.SelectedFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "feat-a" };

            var result = InstallCoordinator.Run(new InstallOperationOptions
            {
                Manifest = manifest,
                ExtractRoot = sourceRoot,
                Destination = installRoot,
                Pal = new TestPal(installRoot),
            });

            File.Exists(Path.Combine(installRoot, "core.txt")).Should().BeTrue();
            File.Exists(Path.Combine(installRoot, "a", "a.txt")).Should().BeTrue();
            File.Exists(Path.Combine(installRoot, "b", "b.txt")).Should().BeFalse();

            result.State.SelectedFeatures.Should().BeEquivalentTo("feat-a");
            result.State.PayloadFiles.Should().BeEquivalentTo("core.txt", "a/a.txt");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(sourceRoot);
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void InstallCoordinator_Update_DeselectingFeature_PrunesPreviousFiles()
    {
        var manifest = BuildManifestWithFeatures();
        var sourceRoot = TestHelpers.NewTempDir();
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(sourceRoot, "core.txt"), "core-v2");
            Directory.CreateDirectory(Path.Combine(sourceRoot, "a"));
            File.WriteAllText(Path.Combine(sourceRoot, "a", "a.txt"), "a-v2");
            Directory.CreateDirectory(Path.Combine(sourceRoot, "b"));
            File.WriteAllText(Path.Combine(sourceRoot, "b", "b.txt"), "b-v2");

            // Seed install state simulating a v1 install that picked feat-a AND feat-b.
            Directory.CreateDirectory(Path.Combine(installRoot, "a"));
            Directory.CreateDirectory(Path.Combine(installRoot, "b"));
            File.WriteAllText(Path.Combine(installRoot, "core.txt"), "core-v1");
            File.WriteAllText(Path.Combine(installRoot, "a", "a.txt"), "a-v1");
            File.WriteAllText(Path.Combine(installRoot, "b", "b.txt"), "b-v1");

            var previousManifest = BuildManifestWithFeatures();
            previousManifest.Metadata.Version = "1.0.0";
            var previousState = TestHelpers.StateFor(previousManifest, installRoot, "1.0.0",
                ["core.txt", "a/a.txt", "b/b.txt"]);
            previousState.SelectedFeatures = ["feat-a", "feat-b"];
            InstallStateIo.WriteState(installRoot, previousState);
            var existing = InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot);

            InstallBootstrap.Init(manifest, sourceRoot, new TestPal(installRoot), existing);
            // Caller deselects feat-b on update.
            InstallBootstrap.SelectedFeatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "feat-a" };

            var result = InstallCoordinator.Run(new InstallOperationOptions
            {
                Manifest = manifest,
                ExtractRoot = sourceRoot,
                Destination = installRoot,
                Pal = new TestPal(installRoot),
                ExistingInstall = existing,
            });

            result.Mode.Should().Be(InstallMode.Update);
            File.ReadAllText(Path.Combine(installRoot, "a", "a.txt")).Should().Be("a-v2");
            File.Exists(Path.Combine(installRoot, "b", "b.txt")).Should().BeFalse();
            File.Exists(Path.Combine(installRoot, "core.txt")).Should().BeTrue();
            result.State.SelectedFeatures.Should().BeEquivalentTo("feat-a");
            result.State.PayloadFiles.Should().BeEquivalentTo("core.txt", "a/a.txt");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(sourceRoot);
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void Bootstrap_Init_SeedsSelectedFeaturesFromExistingState()
    {
        var manifest = BuildManifestWithFeatures();
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            var state = TestHelpers.StateFor(manifest, installRoot, "0.9.0");
            state.SelectedFeatures = ["feat-b"];
            InstallStateIo.WriteState(installRoot, state);
            var existing = InstalledProductLocator.TryReadFromInstallDirectory(manifest, installRoot);

            InstallBootstrap.Init(manifest, installRoot, new TestPal(installRoot), existing);

            InstallBootstrap.SelectedFeatures.Should().BeEquivalentTo("feat-b");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    [Fact]
    public void Bootstrap_Init_SeedsDefaultSelectedOnFreshInstall()
    {
        var manifest = BuildManifestWithFeatures();
        InstallBootstrap.Init(manifest, Path.GetTempPath(), new TestPal(Path.GetTempPath()));

        // feat-a is default_selected=true, feat-b is default_selected=false.
        InstallBootstrap.SelectedFeatures.Should().BeEquivalentTo("feat-a");
    }

    private static InstallManifest BuildManifestWithFeatures()
    {
        return new InstallManifest
        {
            Metadata = new ManifestMetadata { Name = "SampleApp", Version = "2.0.0", Publisher = "Example" },
            Build = new BuildConfiguration
            {
                Targets = ["windows-x64"],
                Windows = new WindowsBuildOptions { InstallScope = "user", RegisterArp = false },
            },
            Features =
            [
                new FeatureDefinition { Id = "feat-a", Name = "Feature A", DefaultSelected = true },
                new FeatureDefinition { Id = "feat-b", Name = "Feature B", DefaultSelected = false },
            ],
            FeatureIndex = new PayloadFeatureIndex
            {
                CoreFiles = ["core.txt"],
                FeatureFiles =
                {
                    ["feat-a"] = ["a/a.txt"],
                    ["feat-b"] = ["b/b.txt"],
                },
            },
        };
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
    }

    private sealed class NoOpShortcutPal : IShortcutPal
    {
        public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath)
        {
        }
    }
}
