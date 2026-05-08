using PolyInstall.Core.Conditions;

namespace PolyInstall.Core.Tests;

public class ConditionEvaluatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Evaluate_EmptyOrWhitespace_IsTrue(string? require)
    {
        ConditionEvaluator.Evaluate(require).Should().BeTrue();
    }

    [Fact]
    public void Evaluate_UnknownCondition_Throws()
    {
        FluentActions.Invoking(() => ConditionEvaluator.Evaluate("custom.script()"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Unknown require condition*");
    }

    [Theory]
    [InlineData("os.is_windows")]
    [InlineData("OS.ISWINDOWS")]
    public void Evaluate_AcceptsSnakeCaseAndCaseInsensitive(string require)
    {
        var expected = OperatingSystem.IsWindows();
        ConditionEvaluator.Evaluate(require).Should().Be(expected);
    }
}
