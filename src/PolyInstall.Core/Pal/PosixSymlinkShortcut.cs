using System.Diagnostics;

namespace PolyInstall.Pal;

internal static class PosixSymlinkShortcut
{
    public static void Create(string targetPath, string shortcutPath)
    {
        var backupPath = shortcutPath + ".polyinstall-bak";
        var hadExistingFile = File.Exists(shortcutPath);
        var hadExistingDir = !hadExistingFile && Directory.Exists(shortcutPath);

        if (hadExistingFile)
            File.Move(shortcutPath, backupPath, overwrite: true);
        else if (hadExistingDir)
            Directory.Move(shortcutPath, backupPath);

        try
        {
            try
            {
                File.CreateSymbolicLink(shortcutPath, targetPath);
            }
            catch (PlatformNotSupportedException)
            {
                File.WriteAllText(shortcutPath, BuildFallbackScript(targetPath));
                Chmod(shortcutPath, 0b111_101_101);
            }

            if (File.Exists(backupPath))
                File.Delete(backupPath);
            else if (Directory.Exists(backupPath))
                Directory.Delete(backupPath, recursive: true);
        }
        catch
        {
            if (File.Exists(shortcutPath))
            {
                try { File.Delete(shortcutPath); } catch { }
            }
            if (File.Exists(backupPath))
                File.Move(backupPath, shortcutPath, overwrite: true);
            else if (Directory.Exists(backupPath))
                Directory.Move(backupPath, shortcutPath);
            throw;
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
