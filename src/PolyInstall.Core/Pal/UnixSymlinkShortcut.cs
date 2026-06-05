using System.Diagnostics;

namespace PolyInstall.Pal;

internal static class UnixSymlinkShortcut
{
    public static void Create(string targetPath, string shortcutPath)
    {
        if (File.Exists(shortcutPath) || Directory.Exists(shortcutPath))
            File.Delete(shortcutPath);
        try
        {
            File.CreateSymbolicLink(shortcutPath, targetPath);
        }
        catch (PlatformNotSupportedException)
        {
            File.WriteAllText(shortcutPath, BuildFallbackScript(targetPath));
            Chmod(shortcutPath, 0b111_101_101);
        }
    }

    public static string BuildFallbackScript(string targetPath)
    {
        return "#!/bin/sh\nexec \"" + targetPath.Replace("\"", "\\\"", StringComparison.Ordinal) + "\" \"$@\"\n";
    }

    private static void Chmod(string path, int mode)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "chmod",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add(Convert.ToString(mode, 8));
        psi.ArgumentList.Add(path);
        using var p = Process.Start(psi);
        p?.WaitForExit();
    }
}
