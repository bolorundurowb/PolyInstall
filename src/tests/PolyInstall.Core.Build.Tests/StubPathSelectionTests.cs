using PolyInstall.Cli.Build;
using PolyInstall.Manifest;
using Assert = Xunit.Assert;

namespace PolyInstall.Core.Build.Tests;

/// <summary>
/// Exercises stub selection rules via <see cref="StubPublishValidator"/> thresholds and
/// documented naming conventions used by <c>InstallerBuildPipeline.ResolveStubPath</c>.
/// </summary>
public class StubPathSelectionTests
{
    [Fact]
    public void IsSilent_ShouldBeTrue_WhenModeIsSilentIgnoringCase()
    {
        new UiConfiguration { Mode = "wizard" }.IsSilent.Must().BeFalse();
        new UiConfiguration { Mode = "silent" }.IsSilent.Must().BeTrue();
        new UiConfiguration { Mode = "SILENT" }.IsSilent.Must().BeTrue();
        new UiConfiguration().IsSilent.Must().BeFalse();
    }

    [Fact]
    public void ValidateRuntimeStub_ShouldAcceptSmallerStub_WhenStubIsSilent()
    {
        var dir = Path.Combine(Path.GetTempPath(), "polyinstall-stub-val-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            var stub = Path.Combine(dir, "PolyInstall.Runtime.Silent.exe");
            // 3 MB: above silent minimum (2 MB), below wizard minimum (5 MB).
            File.WriteAllBytes(stub, new byte[3 * 1024 * 1024]);

            StubPublishValidator.ValidateRuntimeStub(stub, silent: true);

            var ex = Assert.Throws<InvalidOperationException>(
                () => StubPublishValidator.ValidateRuntimeStub(stub, silent: false));
            ex.Message.Must().Contain("too small");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
        }
    }
}
