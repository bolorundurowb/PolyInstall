namespace PolyInstall.Cli.Build;

/// <summary>Console logging for the installer build pipeline.</summary>
internal static class BuildLog
{
    private static bool _verbose;

    public static bool Verbose
    {
        get => _verbose;
        set => _verbose = value;
    }

    public static void Info(string message) => Console.WriteLine($"polyinstall: {message}");

    public static void VerboseLine(string message)
    {
        if (_verbose)
            Info(message);
    }

    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var i = 0;
        while (size >= 1024 && i < units.Length - 1)
        {
            size /= 1024;
            i++;
        }

        return i == 0 ? $"{bytes} B" : $"{size:0.##} {units[i]}";
    }
}
