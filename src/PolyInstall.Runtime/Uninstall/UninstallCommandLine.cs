namespace PolyInstall.Runtime.Uninstall;

internal sealed class UninstallCommandLine
{
    public bool Quiet { get; init; }
    public string? InstallLocation { get; init; }

    public static bool TryParse(string[] args, out UninstallCommandLine? parsed)
    {
        parsed = null;
        var uninstall = false;
        var quiet = false;
        string? installLocation = null;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)
                || a.Equals("--polyinstall-uninstall", StringComparison.OrdinalIgnoreCase))
            {
                uninstall = true;
                continue;
            }

            if (a.Equals("--quiet", StringComparison.OrdinalIgnoreCase))
            {
                quiet = true;
                continue;
            }

            if (a.Equals("--install-location", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                installLocation = args[++i];
            }
        }

        if (!uninstall)
            return false;

        parsed = new UninstallCommandLine { Quiet = quiet, InstallLocation = installLocation };
        return true;
    }
}
