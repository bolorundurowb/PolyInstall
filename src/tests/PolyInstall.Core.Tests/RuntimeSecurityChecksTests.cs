using System.IO.Compression;
using System.Text;
using PolyInstall.Hosting;
using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;
using PolyInstall.Payload;

namespace PolyInstall.Core.Tests;

public class RuntimeSecurityChecksTests
{
    [Fact]
    public void InstallPathPolicy_ShouldRejectVolumeRoot_WhenDestinationIsADriveRoot()
    {
        var volumeRoot = Path.GetPathRoot(Path.GetTempPath());
        if (string.IsNullOrWhiteSpace(volumeRoot))
            return;

        InstallPathPolicy.IsDangerousInstallRoot(volumeRoot).Must().BeTrue();
    }

    [Fact]
    public void InstallPathPolicy_ShouldRejectAncestorOfTheUserProfile_WhenPathContainsTheProfile()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(profile));
        if (string.IsNullOrWhiteSpace(parent))
            return;

        InstallPathPolicy.IsDangerousInstallRoot(parent).Must().BeTrue();
    }

    [Fact]
    public void InstallPathPolicy_ShouldAllowANormalDirectory_WhenPathIsUnderTemp()
    {
        var dir = TestHelpers.NewTempDir();
        try
        {
            InstallPathPolicy.IsDangerousInstallRoot(dir).Must().BeFalse();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public void InstallCoordinator_Run_ShouldThrow_WhenDestinationIsAVolumeRoot()
    {
        var volumeRoot = Path.GetPathRoot(Path.GetTempPath());
        if (string.IsNullOrWhiteSpace(volumeRoot))
            return;

        var act = () => InstallCoordinator.Run(new InstallOperationOptions
        {
            Manifest = TestHelpers.Manifest("SampleApp", "1.0.0"),
            Destination = volumeRoot,
            Pal = new PathOnlyPal(),
            ExtractRoot = Path.GetTempPath(),
        });

        act.Throws<InvalidOperationException>()
            .WithMessageContaining("unsafe install directory");
    }

    [Fact]
    public void RelativePathGuard_ShouldRejectTraversal_WhenNameContainsParentSegments()
    {
        RelativePathGuard.IsSimpleFileName("../evil").Must().BeFalse();
        RelativePathGuard.IsSimpleRelativePath("nested/ok").Must().BeTrue();

        var act = () => RelativePathGuard.CombineConfined(Path.GetTempPath(), "..", "outside");
        act.Throws<InvalidOperationException>()
            .WithMessageContaining("escapes its intended base directory");
    }

    [Fact]
    public void TaskEngine_RunPhase_ShouldThrow_WhenShortcutNameEscapesTheShortcutDirectory()
    {
        InstallBootstrap.Init(TestHelpers.Manifest("SampleApp", "1.0.0"), Path.GetTempPath(), new PathOnlyPal());
        var act = () => TaskEngine.RunPhase(
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["target_path"] = "app",
                        ["name"] = "../evil",
                        ["location"] = "desktop",
                    },
                },
            ],
            new PathOnlyPal());

        act.Throws<InvalidOperationException>()
            .WithMessageContaining("simple file name");
    }

    [Fact]
    public void RuntimeManifestGuard_Validate_ShouldThrow_WhenServiceNameContainsASeparator()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "1.0.0");
        manifest.Services =
        [
            new ServiceDefinition { Name = "bad/name", Executable = "app" },
        ];

        var act = () => RuntimeManifestGuard.Validate(manifest);
        act.Throws<InvalidOperationException>()
            .WithMessageContaining("unsupported characters");
    }

    [Fact]
    public void ZipPayloadExtractor_ExtractStreamToDirectory_ShouldThrow_WhenEntryCountExceedsTheCap()
    {
        using var zipStream = new MemoryStream();
        using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            zip.CreateEntry("one.txt").Open().Dispose();
            zip.CreateEntry("two.txt").Open().Dispose();
        }

        zipStream.Position = 0;
        var dest = TestHelpers.NewTempDir();
        try
        {
            var act = () => ZipPayloadExtractor.ExtractStreamToDirectory(zipStream, dest, maxEntries: 1);
            act.Throws<InvalidDataException>()
                .WithMessageContaining("more than 1 entries");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dest);
        }
    }

    [Fact]
    public void InstallPayloadLimits_CopyWithLimit_ShouldThrow_WhenStreamExceedsTheCap()
    {
        using var source = new MemoryStream(Encoding.UTF8.GetBytes("abcdef"));
        using var destination = new MemoryStream();
        Action act = () => InstallPayloadLimits.CopyWithLimit(source, destination, maxBytes: 3);
        act.Throws<InvalidDataException>()
            .WithMessageContaining("maximum supported decompressed size");
    }

    [Fact]
    public void WindowsServiceOwnership_TryExtractBinaryPath_ShouldReturnTheQuotedBinary_WhenScOutputContainsIt()
    {
        var output = "BINARY_PATH_NAME   : \"C:\\Program Files\\Sample\\app.exe\" --flag";
        WindowsServiceOwnership.TryExtractBinaryPath(output).Must().Be(@"C:\Program Files\Sample\app.exe");
    }

    [Fact]
    public void PosixPathPal_BuildPathExportEntry_ShouldSingleQuoteTheDirectory_WhenItContainsShellMetacharacters()
    {
        var entry = PosixPathPal.BuildPathExportEntry("/opt/sample's bin");
        entry.Must().Be("export PATH=\"$PATH\":'/opt/sample'\\''s bin'");
    }

    [Fact]
    public void ShouldRelaunchElevated_ShouldReturnFalse_WhenStateClaimsAnUnverifiedSystemService()
    {
        if (OperatingSystem.IsWindows())
            return;

        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0", installScope: "user");
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            var state = TestHelpers.StateFor(manifest, installRoot, "1.0.0");
            var existing = new ExistingInstallInfo
            {
                InstallLocation = installRoot,
                InstallScope = "user",
                State = state,
            };
            state.RegisteredServices =
            [
                new RegisteredServiceInfo
                {
                    Name = "Spooler",
                    Scope = "system",
                    Platform = "windows",
                },
            ];

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

    private sealed class PathOnlyPal : IPolyInstallPal
    {
        public string AppDir => System.IO.Path.GetTempPath();
        public string ProgramFiles => System.IO.Path.GetTempPath();
        public string LocalAppData => System.IO.Path.GetTempPath();
        public string UserHome => System.IO.Path.GetTempPath();
        public string Desktop => System.IO.Path.GetTempPath();
        public IShortcutPal Shortcuts { get; } = new SilentShortcutPal();
        public IRegistryPal? Registry => null;
        public IDesktopEntryPal? DesktopEntries => null;
        public IFilePermissionsPal? FilePermissions => null;
        public IPathPal? Path => null;
        public IFileAssociationPal? FileAssociations => null;
        public IServiceManagerPal? Services => null;
        public IProcessManagerPal Processes { get; } = new NoOpProcessManagerPal();
        public IElevationPal Elevation { get; } = new NoOpElevationPal();
    }

    private sealed class SilentShortcutPal : IShortcutPal
    {
        public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath)
        {
        }
    }
}
