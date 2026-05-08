using PolyInstall.Core.Install;

namespace PolyInstall.Uninstall;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (!UninstallCommandLine.TryParse(args, out var uninstallCmd) || uninstallCmd is null)
            return 1;

        return UninstallRunner.Run(uninstallCmd);
    }
}
