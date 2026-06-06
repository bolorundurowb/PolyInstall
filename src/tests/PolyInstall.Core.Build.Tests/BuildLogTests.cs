using PolyInstall.Cli.Build;

namespace PolyInstall.Core.Build.Tests;

public class BuildLogTests
{
    [Fact]
    public void Info_WhenQuiet_DoesNotWriteToConsole()
    {
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            BuildLog.Quiet = true;
            BuildLog.Verbose = false;
            BuildLog.Info("should not appear");
            BuildLog.VerboseLine("also should not appear");

            sw.ToString().Should().BeEmpty();
        }
        finally
        {
            Console.SetOut(originalOut);
            BuildLog.Quiet = false;
        }
    }

    [Fact]
    public void Info_WhenNotQuiet_WritesToConsole()
    {
        var originalOut = Console.Out;
        try
        {
            using var sw = new StringWriter();
            Console.SetOut(sw);

            BuildLog.Quiet = false;
            BuildLog.Verbose = false;
            BuildLog.Info("hello");

            sw.ToString().Should().Contain("hello");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
