using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text;

namespace PolyInstall.Pal;

internal static class WindowsShortcut
{
    [SupportedOSPlatform("windows")]
    public static void Create(string targetPath, string shortcutPath, string? description, string? iconPath)
    {
        var script = new StringBuilder();
        script.Append("$w = New-Object -ComObject WScript.Shell; ");
        script.Append("$s = $w.CreateShortcut(" + PsQ(shortcutPath) + "); ");
        script.Append("$s.TargetPath = " + PsQ(targetPath) + "; ");
        if (!string.IsNullOrEmpty(description))
            script.Append("$s.Description = " + PsQ(description) + "; ");
        if (!string.IsNullOrEmpty(iconPath))
            script.Append("$s.IconLocation = " + PsQ(iconPath) + "; ");
        script.Append("$s.Save()");

        var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script.ToString()));
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {enc}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi);
        p?.WaitForExit();
        if (p?.ExitCode != 0)
        {
            var stdout = p?.StandardOutput.ReadToEnd() ?? "";
            var stderr = p?.StandardError.ReadToEnd() ?? "";
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            if (!string.IsNullOrWhiteSpace(detail))
                throw new InvalidOperationException($"Shortcut creation failed (exit {p?.ExitCode}): {detail.Trim()}");
            throw new InvalidOperationException($"Shortcut creation failed (exit {p?.ExitCode}).");
        }
    }

    public static string BuildPowerShellScript(string targetPath, string shortcutPath, string? description, string? iconPath)
    {
        var script = new StringBuilder();
        script.Append("$w = New-Object -ComObject WScript.Shell; ");
        script.Append("$s = $w.CreateShortcut(" + PsQ(shortcutPath) + "); ");
        script.Append("$s.TargetPath = " + PsQ(targetPath) + "; ");
        if (!string.IsNullOrEmpty(description))
            script.Append("$s.Description = " + PsQ(description) + "; ");
        if (!string.IsNullOrEmpty(iconPath))
            script.Append("$s.IconLocation = " + PsQ(iconPath) + "; ");
        script.Append("$s.Save()");
        return script.ToString();
    }

    private static string PsQ(string s) => "'" + s.Replace("'", "''", StringComparison.Ordinal) + "'";
}
