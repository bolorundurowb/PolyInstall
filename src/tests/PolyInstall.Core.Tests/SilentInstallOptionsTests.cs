using PolyInstall.Hosting;

namespace PolyInstall.Core.Tests;

public class SilentInstallOptionsTests
{
    [Fact]
    public void TryParse_ShouldSucceedWithDefaults_WhenArgsAreEmpty()
    {
        SilentInstallOptions.TryParse([], out var options, out var error).Must().BeTrue();
        error.Must().BeNull();
        options.Silent.Must().BeFalse();
        options.AcceptEula.Must().BeFalse();
    }

    [Fact]
    public void TryParse_ShouldPopulateAllOptions_WhenAllFlagsAreSupplied()
    {
        var installDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "polyinstall-opt-test"));
        var logPath = Path.Combine(installDir, "install.log");
        SilentInstallOptions.TryParse(
            [
                "--silent",
                "--install-dir", installDir,
                "--features", "docs,extra",
                "--accept-eula",
                "--close-apps",
                "--log", logPath,
            ],
            out var options,
            out var error).Must().BeTrue();
        error.Must().BeNull();
        options.Silent.Must().BeTrue();
        options.InstallDir.Must().Be(installDir);
        options.Features.Must().Be("docs,extra");
        options.AcceptEula.Must().BeTrue();
        options.CloseApps.Must().BeTrue();
        options.LogPath.Must().Be(logPath);
    }

    [Fact]
    public void TryParse_RelativeInstallDir_Fails()
    {
        SilentInstallOptions.TryParse(["--install-dir", "relative/path"], out _, out var error)
            .Must().BeFalse();
        error.Must().Contain("absolute");
    }

    [Fact]
    public void TryParse_InstallDirWithDotDot_Fails()
    {
        var rooted = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "a", "..", "b"));
        // Even if GetFullPath collapses '..', the raw argument with '..' segments is rejected.
        var raw = Path.Combine(Path.GetPathRoot(Path.GetTempPath())!, "tmp", "..", "evil");
        SilentInstallOptions.TryParse(["--install-dir", raw], out _, out var error)
            .Must().BeFalse();
        error.Must().Contain("..");
    }

    [Fact]
    public void TryParse_UnknownFlag_Fails()
    {
        SilentInstallOptions.TryParse(["--not-real"], out _, out var error).Must().BeFalse();
        error.Must().Contain("Unrecognised");
    }

    [Fact]
    public void TryParse_Help_Succeeds()
    {
        SilentInstallOptions.TryParse(["--help"], out var options, out _).Must().BeTrue();
        options.Help.Must().BeTrue();
    }

    [Fact]
    public void ValidateInstallDir_AbsolutePath_ReturnsNull()
    {
        var path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ok"));
        SilentInstallOptions.ValidateInstallDir(path).Must().BeNull();
    }

    [Fact]
    public void BuildForwardedArgs_IncludesLogAppend()
    {
        var options = new SilentInstallOptions
        {
            Silent = true,
            AcceptEula = true,
            InstallDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "x")),
        };
        var args = SilentInstallRunner.BuildForwardedArgs(options, "/tmp/log");
        args.Must().Contain("--silent");
        args.Must().Contain("--log");
        args.Must().Contain("--log-append");
        args.Must().Contain("--accept-eula");
        args.Must().Contain("/tmp/log");
    }
}
