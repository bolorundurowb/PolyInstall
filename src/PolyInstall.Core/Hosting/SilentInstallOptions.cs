using System.Globalization;

namespace PolyInstall.Hosting;

public sealed class SilentInstallOptions
{
    public bool Silent { get; init; }
    public string? InstallDir { get; init; }
    public string? Features { get; init; }
    public bool AcceptEula { get; init; }
    public bool CloseApps { get; init; }
    public string? LogPath { get; init; }
    /// <summary>
    /// When true, open an existing <see cref="LogPath"/> for append (used by the elevated child
    /// after the parent created the file). Not a documented operator flag.
    /// </summary>
    public bool LogAppend { get; init; }
    public bool Help { get; init; }

    /// <summary>
    /// Parse silent-install CLI arguments. Returns null and an error message on invalid input.
    /// </summary>
    public static bool TryParse(string[] args, out SilentInstallOptions options, out string? error)
    {
        options = new SilentInstallOptions();
        error = null;

        var silent = false;
        string? installDir = null;
        string? features = null;
        var acceptEula = false;
        var closeApps = false;
        string? logPath = null;
        var logAppend = false;
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals("--help", StringComparison.OrdinalIgnoreCase)
                || a.Equals("-h", StringComparison.OrdinalIgnoreCase)
                || a.Equals("/?", StringComparison.OrdinalIgnoreCase))
            {
                help = true;
                continue;
            }

            if (a.Equals("--silent", StringComparison.OrdinalIgnoreCase)
                || a.Equals("/S", StringComparison.OrdinalIgnoreCase)
                || a.Equals("/silent", StringComparison.OrdinalIgnoreCase))
            {
                silent = true;
                continue;
            }

            if (a.Equals("--accept-eula", StringComparison.OrdinalIgnoreCase))
            {
                acceptEula = true;
                continue;
            }

            if (a.Equals("--close-apps", StringComparison.OrdinalIgnoreCase))
            {
                closeApps = true;
                continue;
            }

            if (a.Equals("--log-append", StringComparison.OrdinalIgnoreCase))
            {
                logAppend = true;
                continue;
            }

            if (a.Equals("--install-dir", StringComparison.OrdinalIgnoreCase)
                || a.Equals("--installdir", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    error = $"{a} requires a path argument.";
                    return false;
                }
                installDir = args[++i];
                continue;
            }

            if (a.Equals("--features", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    error = "--features requires a value (comma-separated ids, 'all', or 'default').";
                    return false;
                }
                features = args[++i];
                continue;
            }

            if (a.Equals("--log", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    error = "--log requires a path argument.";
                    return false;
                }
                logPath = args[++i];
                continue;
            }

            error = $"Unrecognised argument: {a}";
            return false;
        }

        if (installDir is not null)
        {
            var validationError = ValidateInstallDir(installDir);
            if (validationError is not null)
            {
                error = validationError;
                return false;
            }
        }

        if (logPath is not null)
        {
            var validationError = ValidateLogPathArgument(logPath);
            if (validationError is not null)
            {
                error = validationError;
                return false;
            }
        }

        options = new SilentInstallOptions
        {
            Silent = silent,
            InstallDir = installDir,
            Features = features,
            AcceptEula = acceptEula,
            CloseApps = closeApps,
            LogPath = logPath,
            LogAppend = logAppend,
            Help = help,
        };
        return true;
    }

    public static string HelpText =>
        """
        PolyInstall silent installer

        Usage:
          <installer> [--silent] [options]

        Options:
          --silent                 Run without the interactive wizard (implied by ui.mode: silent stubs).
          --install-dir <path>     Absolute install directory (no '..' segments).
          --features <a,b|all|default>
                                   Feature selection. Default uses the seed from the manifest / prior install.
          --accept-eula            Required when the manifest includes an eula wizard step.
          --close-apps             Terminate processes running from the install directory, then continue.
          --log <path>             Write a timestamped log. Must not already exist; must not be a symlink.
          --help                   Show this help and exit 0.

        Exit codes:
          0  Success
          1  Unexpected error
          2  Invalid arguments
          3  Elevation denied, unsupported, or failed
          4  Blocking processes still running
          5  Cancelled
          6  EULA not accepted
          7  An existing install blocks this one
        """;

    /// <summary>
    /// Validate <paramref name="path"/> as an absolute install directory with no traversal
    /// segments and no control characters. Called in the parent process before any elevation
    /// relaunch so the path is vetted across the privilege boundary.
    /// </summary>
    public static string? ValidateInstallDir(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "--install-dir must not be empty.";
        if (path.Any(char.IsControl))
            return "--install-dir must not contain control characters.";
        if (!Path.IsPathRooted(path))
            return "--install-dir must be an absolute path.";

        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s == ".." || s == "."))
            return "--install-dir must not contain '.' or '..' path segments.";

        try
        {
            _ = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            return $"--install-dir is not a usable path: {ex.Message}";
        }

        return null;
    }

    /// <summary>
    /// Validate the operator-supplied <c>--log</c> argument before elevation. Existence and
    /// symlink checks happen later when the log sink is opened (the file must not exist yet).
    /// </summary>
    public static string? ValidateLogPathArgument(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "--log must not be empty.";
        if (path.Any(char.IsControl))
            return "--log must not contain control characters.";
        if (!Path.IsPathRooted(path))
            return "--log must be an absolute path.";

        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s == ".."))
            return "--log must not contain '..' path segments.";

        try
        {
            _ = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            return $"--log is not a usable path: {ex.Message}";
        }

        return null;
    }

    public static string FormatTimestamp(DateTimeOffset utc) =>
        utc.ToString("O", CultureInfo.InvariantCulture);
}
