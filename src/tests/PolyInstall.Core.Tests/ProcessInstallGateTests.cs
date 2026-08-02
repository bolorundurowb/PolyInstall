using PolyInstall.Pal;

namespace PolyInstall.Core.Tests;

public class ProcessInstallGateTests
{
    [Fact]
    public void IsUnderDirectory_ReturnsTrue_ForExecutableUnderDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "poly-gate", "App");
        var exe = Path.Combine(dir, "bin", "app.exe");

        ProcessPathMatcher.IsUnderDirectory(exe, dir).Must().BeTrue();
    }

    [Fact]
    public void IsUnderDirectory_ReturnsFalse_ForSiblingPath()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), "poly-gate");
        var dir = Path.Combine(baseDir, "App");
        var siblingExe = Path.Combine(baseDir, "AppExtra", "app.exe");

        ProcessPathMatcher.IsUnderDirectory(siblingExe, dir).Must().BeFalse();
    }

    [Fact]
    public void IsUnderDirectory_ReturnsFalse_WhenExecutableIsTheDirectoryItself()
    {
        var dir = Path.Combine(Path.GetTempPath(), "poly-gate", "App");

        ProcessPathMatcher.IsUnderDirectory(dir, dir).Must().BeFalse();
    }

    [Fact]
    public void IsUnderDirectory_IsCaseInsensitive()
    {
        var dir = Path.Combine(Path.GetTempPath(), "poly-gate", "App");
        var exe = Path.Combine(dir.ToUpperInvariant(), "bin", "app.exe");

        ProcessPathMatcher.IsUnderDirectory(exe, dir).Must().BeTrue();
    }

    [Theory]
    [InlineData(null, "C:/dir")]
    [InlineData("", "C:/dir")]
    [InlineData("  ", "C:/dir")]
    [InlineData("C:/dir/app.exe", null)]
    [InlineData("C:/dir/app.exe", "")]
    public void IsUnderDirectory_ReturnsFalse_ForEmptyInputs(string? executablePath, string? directory)
    {
        ProcessPathMatcher.IsUnderDirectory(executablePath, directory).Must().BeFalse();
    }

    [Fact]
    public void FindProcessesUnderDirectory_ReturnsEmpty_ForMissingDirectory()
    {
        var pal = new ProcessManagerPal();
        var missing = Path.Combine(Path.GetTempPath(), "poly-gate-missing-" + Guid.NewGuid().ToString("n"));

        pal.FindProcessesUnderDirectory(missing).Must().BeEmpty();
    }

    [Fact]
    public void FindProcessesUnderDirectory_ReturnsEmpty_ForBlankDirectory()
    {
        var pal = new ProcessManagerPal();

        pal.FindProcessesUnderDirectory("   ").Must().BeEmpty();
    }

    [Fact]
    public void TerminateAndRescan_TerminatesAndReportsClear_WhenNoneRemain()
    {
        var running = new[]
        {
            new RunningProcessInfo(101, "app", @"C:\App\app.exe"),
            new RunningProcessInfo(102, "helper", @"C:\App\helper.exe"),
        };
        var pal = new FakeProcessManagerPal(afterTerminate: []);

        var remaining = InstallProcessGuard.TerminateAndRescan(pal, running, @"C:\App");

        pal.Terminated.Must().HaveCount(2);
        pal.Terminated.Contains(101).Must().BeTrue();
        pal.Terminated.Contains(102).Must().BeTrue();
        remaining.Must().BeEmpty();
    }

    [Fact]
    public void TerminateAndRescan_ReturnsRemaining_WhenProcessesSurvive()
    {
        var survivor = new RunningProcessInfo(200, "stubborn", @"C:\App\stubborn.exe");
        var pal = new FakeProcessManagerPal(afterTerminate: [survivor]);

        var remaining = InstallProcessGuard.TerminateAndRescan(
            pal,
            [survivor],
            @"C:\App");

        pal.Terminated.Must().HaveCount(1);
        remaining.Must().HaveCount(1);
        remaining[0].Id.Must().Be(200);
    }

    private sealed class FakeProcessManagerPal(IReadOnlyList<RunningProcessInfo> afterTerminate) : IProcessManagerPal
    {
        public List<int> Terminated { get; } = [];

        public IReadOnlyList<RunningProcessInfo> FindProcessesUnderDirectory(string directory) => afterTerminate;

        public void Terminate(IEnumerable<int> processIds, string mustBeUnderDirectory) => Terminated.AddRange(processIds);
    }
}
