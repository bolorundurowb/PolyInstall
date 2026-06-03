using PolyInstall.Core.Conditions;

namespace PolyInstall.Core.Tests;

public class ConditionEvaluatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_WithNullOrWhitespaceRequire_ReturnsTrue(string? require)
    {
        ConditionEvaluator.Evaluate(require).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_WithUnknownCondition_ThrowsNotSupportedException()
    {
        FluentActions.Invoking(() => ConditionEvaluator.Evaluate("custom.script()"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Unknown require condition*");
    }

    [Theory]
    [InlineData("os.isWindows", "windows")]
    [InlineData("os.isLinux", "linux")]
    public void Evaluate_WithCamelCaseOsPredicates_EvaluatesCorrectly(string require, string osFamily)
    {
        var expected = osFamily switch
        {
            "windows" => OperatingSystem.IsWindows(),
            "linux" => OperatingSystem.IsLinux(),
            _ => throw new ArgumentOutOfRangeException(nameof(osFamily), osFamily, null),
        };
        ConditionEvaluator.Evaluate(require).Should().Be(expected);
    }

    [Theory]
    [InlineData("os.is_windows", "windows")]
    [InlineData("OS.ISWINDOWS", "windows")]
    [InlineData("os.is_linux", "linux")]
    [InlineData("os.is_osx", "macos")]
    [InlineData("os.is_macos", "macos")]
    [InlineData("os.is_unix", "unix")]
    public void Evaluate_WithSnakeCaseOrDifferentCasing_EvaluatesOsPredicate(string require, string osFamily)
    {
        var expected = osFamily switch
        {
            "windows" => OperatingSystem.IsWindows(),
            "linux" => OperatingSystem.IsLinux(),
            "macos" => OperatingSystem.IsMacOS(),
            "unix" => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD(),
            _ => throw new ArgumentOutOfRangeException(nameof(osFamily), osFamily, null),
        };
        ConditionEvaluator.Evaluate(require).Should().Be(expected);
    }
}
