using PolyInstall.Hosting;
using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;
using Assert = Xunit.Assert;

namespace PolyInstall.Core.Tests;

public class SilentInstallRunnerTests
{
    [Fact]
    public void Run_ShouldReturnSuccess_WhenHelpIsRequested()
    {
        var stdout = new StringWriter();
        var code = SilentInstallRunner.Run(
            TestHelpers.Manifest("HelpApp", "1.0.0"),
            Path.GetTempPath(),
            new TestPal(Path.GetTempPath()),
            existingInstall: null,
            new SilentInstallOptions { Help = true },
            stdout: stdout);
        code.Must().Be(SilentExitCodes.Success);
        stdout.ToString().Must().Contain("Exit codes");
    }

    [Fact]
    public void Run_ShouldReturnEulaNotAccepted_WhenEulaIsNotAccepted()
    {
        var extract = TestHelpers.NewTempDir();
        var installDir = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(extract, "payload.txt"), "x");
            var manifest = TestHelpers.Manifest("EulaApp", "1.0.0");
            manifest.Ui.WizardSteps =
            [
                new WizardStep { Type = "eula", Source = "eula.txt" },
                new WizardStep { Type = "destination", DefaultPath = installDir },
                new WizardStep { Type = "progress" },
            ];
            var code = SilentInstallRunner.Run(
                manifest,
                extract,
                new TestPal(installDir),
                existingInstall: null,
                new SilentInstallOptions
                {
                    Silent = true,
                    InstallDir = installDir,
                },
                stdout: TextWriter.Null);
            code.Must().Be(SilentExitCodes.EulaNotAccepted);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(extract);
            TestHelpers.TryDeleteDirectory(installDir);
        }
    }

    [Fact]
    public void Run_ShouldInstallPayload_WhenInstallIsFresh()
    {
        var extract = TestHelpers.NewTempDir();
        var installDir = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(extract, "hello.txt"), "hi");
            var manifest = TestHelpers.Manifest("SilentApp", "1.0.0");
            manifest.Ui.Mode = "silent";
            manifest.Ui.WizardSteps =
            [
                new WizardStep { Type = "destination", DefaultPath = installDir },
                new WizardStep { Type = "progress" },
            ];
            var code = SilentInstallRunner.Run(
                manifest,
                extract,
                new TestPal(installDir),
                existingInstall: null,
                new SilentInstallOptions
                {
                    Silent = true,
                    InstallDir = installDir,
                },
                stdout: TextWriter.Null);
            code.Must().Be(SilentExitCodes.Success);
            File.Exists(Path.Combine(installDir, "hello.txt")).Must().BeTrue();
            File.Exists(InstallStatePaths.InstallStatePath(installDir)).Must().BeTrue();
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(extract);
            TestHelpers.TryDeleteDirectory(installDir);
        }
    }

    [Fact]
    public void Run_ShouldReturnInvalidArguments_WhenFeatureIsUnknown()
    {
        var extract = TestHelpers.NewTempDir();
        var installDir = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(extract, "hello.txt"), "hi");
            var manifest = TestHelpers.Manifest("FeatApp", "1.0.0");
            manifest.Features = [new FeatureDefinition { Id = "docs", Name = "Docs", DefaultSelected = true }];
            var code = SilentInstallRunner.Run(
                manifest,
                extract,
                new TestPal(installDir),
                existingInstall: null,
                new SilentInstallOptions
                {
                    Silent = true,
                    InstallDir = installDir,
                    Features = "nope",
                },
                stdout: TextWriter.Null);
            code.Must().Be(SilentExitCodes.InvalidArguments);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(extract);
            TestHelpers.TryDeleteDirectory(installDir);
        }
    }

    [Fact]
    public void Run_ShouldReturnProcessesRunning_WhenProcessesRunWithoutCloseApps()
    {
        var extract = TestHelpers.NewTempDir();
        var installDir = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(extract, "hello.txt"), "hi");
            var processes = new FakeBusyProcessPal(
            [
                new RunningProcessInfo(42, "busy", Path.Combine(installDir, "busy.exe")),
            ]);
            var code = SilentInstallRunner.Run(
                TestHelpers.Manifest("BusyApp", "1.0.0"),
                extract,
                new TestPal(installDir, processes),
                existingInstall: null,
                new SilentInstallOptions
                {
                    Silent = true,
                    InstallDir = installDir,
                },
                stdout: TextWriter.Null);
            code.Must().Be(SilentExitCodes.ProcessesRunning);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(extract);
            TestHelpers.TryDeleteDirectory(installDir);
        }
    }

    [Fact]
    public void Run_ShouldReturnCancelled_WhenCancellationIsRequested()
    {
        var extract = TestHelpers.NewTempDir();
        var installDir = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(extract, "hello.txt"), "hi");
            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var code = SilentInstallRunner.Run(
                TestHelpers.Manifest("CancelApp", "1.0.0"),
                extract,
                new TestPal(installDir),
                existingInstall: null,
                new SilentInstallOptions
                {
                    Silent = true,
                    InstallDir = installDir,
                },
                cts.Token,
                TextWriter.Null);
            code.Must().Be(SilentExitCodes.Cancelled);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(extract);
            TestHelpers.TryDeleteDirectory(installDir);
        }
    }

    [Fact]
    public void Run_ShouldReturnElevationRequired_WhenElevationIsDenied()
    {
        if (!OperatingSystem.IsWindows())
            return; // ShouldRelaunchElevated is Windows-only

        var extract = TestHelpers.NewTempDir();
        var installDir = TestHelpers.NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(extract, "hello.txt"), "hi");
            var manifest = TestHelpers.Manifest("MachineApp", "1.0.0", installScope: "machine");
            var elevation = new FakeElevationPal(isElevated: false, ElevationResult.Denied());
            var code = SilentInstallRunner.Run(
                manifest,
                extract,
                new TestPal(installDir, elevation: elevation),
                existingInstall: null,
                new SilentInstallOptions
                {
                    Silent = true,
                    InstallDir = installDir,
                },
                stdout: TextWriter.Null);
            code.Must().Be(SilentExitCodes.ElevationRequired);
            elevation.RelaunchAndWaitCalls.Must().Be(1);
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(extract);
            TestHelpers.TryDeleteDirectory(installDir);
        }
    }

    [Fact]
    public void Open_ShouldThrow_WhenLogFileAlreadyExists()
    {
        var dir = TestHelpers.NewTempDir();
        try
        {
            var logPath = Path.Combine(dir, "a.log");
            using (var sink = SilentLogSink.Open(logPath))
            {
                sink.WriteLine("first");
                File.Exists(sink.Path).Must().BeTrue();
            }

            Assert.Throws<InvalidOperationException>(() => SilentLogSink.Open(logPath));
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dir);
        }
    }

    [Fact]
    public void Open_ShouldAppendToLog_WhenAppendIsTrue()
    {
        var dir = TestHelpers.NewTempDir();
        try
        {
            var logPath = Path.Combine(dir, "a.log");
            using (var sink = SilentLogSink.Open(logPath))
                sink.WriteLine("first");

            using (var sink = SilentLogSink.Open(logPath, append: true))
                sink.WriteLine("second");

            var text = File.ReadAllText(logPath);
            text.Must().Contain("first");
            text.Must().Contain("second");
        }
        finally
        {
            TestHelpers.TryDeleteDirectory(dir);
        }
    }

    private sealed class FakeBusyProcessPal(IReadOnlyList<RunningProcessInfo> running) : IProcessManagerPal
    {
        public IReadOnlyList<RunningProcessInfo> FindProcessesUnderDirectory(string directory) => running;
        public void Terminate(IEnumerable<int> processIds) { }
    }

    private sealed class FakeElevationPal(bool isElevated, ElevationResult waitResult) : IElevationPal
    {
        public int RelaunchAndWaitCalls { get; private set; }
        public bool IsElevated { get; } = isElevated;

        public ElevationResult Relaunch(string exe, IReadOnlyList<string> args) =>
            ElevationResult.Unsupported();

        public ElevationResult RelaunchAndWait(string exe, IReadOnlyList<string> args)
        {
            RelaunchAndWaitCalls++;
            return waitResult;
        }
    }

    private sealed class TestPal(
        string appDir,
        IProcessManagerPal? processes = null,
        IElevationPal? elevation = null) : IPolyInstallPal
    {
        public string AppDir => appDir;
        public string ProgramFiles => appDir;
        public string LocalAppData => appDir;
        public string UserHome => appDir;
        public string Desktop => appDir;
        public IShortcutPal Shortcuts { get; } = new NoOpShortcutPal();
        public IRegistryPal? Registry => null;
        public IDesktopEntryPal? DesktopEntries => null;
        public IFilePermissionsPal? FilePermissions => null;
        public IFileAssociationPal? FileAssociations => null;
        public IPathPal? Path => null;
        public IServiceManagerPal? Services => null;
        public IProcessManagerPal Processes { get; } = processes ?? new NoOpProcessManagerPal();
        public IElevationPal Elevation { get; } = elevation ?? new NoOpElevationPal();
    }

    private sealed class NoOpShortcutPal : IShortcutPal
    {
        public void CreateFileShortcut(string targetPath, string shortcutPath, string? description, string? iconPath) { }
    }
}
