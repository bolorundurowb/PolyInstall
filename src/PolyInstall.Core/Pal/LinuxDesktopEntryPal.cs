namespace PolyInstall.Pal;

internal sealed class LinuxDesktopEntryPal : IDesktopEntryPal
{
    public void CreateDesktopEntry(string fileName, string name, string exec, string? icon, string? comment)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName.EndsWith(".desktop", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".desktop");
        var content = BuildDesktopEntryContent(name, exec, icon, comment);
        File.WriteAllText(path, content);
    }

    public static string BuildDesktopEntryContent(string name, string exec, string? icon, string? comment)
    {
        var lines = new List<string>
        {
            "[Desktop Entry]",
            "Type=Application",
            $"Name={name}",
            $"Exec={exec}",
            "Terminal=false",
        };
        if (!string.IsNullOrEmpty(icon))
            lines.Add($"Icon={icon}");
        if (!string.IsNullOrEmpty(comment))
            lines.Add($"Comment={comment}");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
