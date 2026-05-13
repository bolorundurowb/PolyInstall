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
    [InlineData("os.is_windows")]
    [InlineData("OS.ISWINDOWS")]
    public void Evaluate_WithSnakeCaseOrDifferentCasing_EvaluatesOsPredicate(string require)
    {
        var expected = OperatingSystem.IsWindows();
        ConditionEvaluator.Evaluate(require).Should().Be(expected);
    }
}
