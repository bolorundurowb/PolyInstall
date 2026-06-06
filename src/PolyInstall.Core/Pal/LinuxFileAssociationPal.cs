using System.Diagnostics;
using System.Runtime.Versioning;
using System.Xml.Linq;
using PolyInstall.Hosting;
using PolyInstall.Install;

namespace PolyInstall.Pal;

internal sealed class LinuxFileAssociationPal : IFileAssociationPal
{
    private static readonly XNamespace MimeNs = "http://www.freedesktop.org/standards/shared-mime-info";

    [SupportedOSPlatform("linux")]
    public void Register(FileAssociationInfo association)
    {
        var mimeType = ResolveMimeType(association);
        var desktopFileName = ResolveDesktopFileName(association);

        Backup(association.Extension, mimeType);

        WriteMimeTypeXml(association, mimeType);
        UpdateDesktopEntryMimeType(desktopFileName, mimeType);
        RunCommand("update-mime-database", GetMimeDatabasePath());
        RunCommand("update-desktop-database", GetApplicationsPath());
        RunCommand("xdg-mime", $"default {desktopFileName} {mimeType}");
    }

    [SupportedOSPlatform("linux")]
    public void Unregister(FileAssociationInfo association)
    {
        var mimeType = ResolveMimeType(association);
        var backup = GetBackup(association.Extension);

        var createdFiles = GetCreatedFiles(association, mimeType);
        foreach (var file in createdFiles)
        {
            try { File.Delete(file); } catch { }
        }

        RunCommand("update-mime-database", GetMimeDatabasePath());
        RunCommand("update-desktop-database", GetApplicationsPath());

        if (backup is not null && !string.IsNullOrEmpty(backup.OriginalDefaultApp))
        {
            RunCommand("xdg-mime", $"default {backup.OriginalDefaultApp} {mimeType}");
        }
    }

    private static string ResolveMimeType(FileAssociationInfo association)
    {
        if (!string.IsNullOrEmpty(association.MimeType))
            return association.MimeType;

        var ext = association.Extension.TrimStart('.').ToLowerInvariant();
        return $"application/x-{ext}";
    }

    private static string ResolveDesktopFileName(FileAssociationInfo association)
    {
        var safeAppName = new string(association.ProgId
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.')
            .ToArray());
        return $"{safeAppName}.desktop";
    }

    [SupportedOSPlatform("linux")]
    private static void WriteMimeTypeXml(FileAssociationInfo association, string mimeType)
    {
        var dir = GetMimePackagesPath();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{association.ProgId.Replace('.', '-')}-{association.Extension.TrimStart('.')}.xml");

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(MimeNs + "mime-info",
                new XElement(MimeNs + "mime-type",
                    new XAttribute("type", mimeType),
                    new XElement(MimeNs + "comment", association.Description),
                    new XElement(MimeNs + "glob", new XAttribute("pattern", $"*{association.Extension}")))));

        doc.Save(path);

        var installDir = InstallBootstrap.InstallDirectory;
        if (!string.IsNullOrEmpty(installDir))
        {
            try
            {
                var state = InstallStateIo.ReadState(installDir);
                state.FileAssociationBackups ??= new List<FileAssociationBackup>();
                var backup = state.FileAssociationBackups.FirstOrDefault(b =>
                    b.Extension.Equals(association.Extension, StringComparison.OrdinalIgnoreCase));
                if (backup is not null)
                {
                    backup.BackupFilePaths ??= new List<string>();
                    if (!backup.BackupFilePaths.Contains(path))
                        backup.BackupFilePaths.Add(path);
                    InstallStateIo.WriteState(installDir, state);
                }
            }
            catch { }
        }
    }

    [SupportedOSPlatform("linux")]
    private static void UpdateDesktopEntryMimeType(string desktopFileName, string mimeType)
    {
        var dir = GetApplicationsPath();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, desktopFileName);

        var lines = new List<string>();
        if (File.Exists(path))
        {
            lines = File.ReadAllLines(path).ToList();
            var mimeLineIndex = lines.FindIndex(l => l.StartsWith("MimeType=", StringComparison.Ordinal));
            if (mimeLineIndex >= 0)
            {
                var existing = lines[mimeLineIndex]["MimeType=".Length..];
                var types = existing.Split(';', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
                if (types.Add(mimeType))
                {
                    lines[mimeLineIndex] = $"MimeType={string.Join(';', types)};";
                }
            }
            else
            {
                lines.Add($"MimeType={mimeType};");
            }
        }

        File.WriteAllLines(path, lines);
    }

    [SupportedOSPlatform("linux")]
    private static void Backup(string extension, string mimeType)
    {
        var installDir = InstallBootstrap.InstallDirectory;
        if (string.IsNullOrEmpty(installDir)) return;

        string? originalDefault = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "xdg-mime",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("query");
            psi.ArgumentList.Add("default");
            psi.ArgumentList.Add(mimeType);
            using var p = Process.Start(psi);
            if (p is not null)
            {
                p.WaitForExit();
                if (p.ExitCode == 0)
                    originalDefault = p.StandardOutput.ReadToEnd().Trim();
            }
        }
        catch { }

        try
        {
            var state = InstallStateIo.ReadState(installDir);
            state.FileAssociationBackups ??= new List<FileAssociationBackup>();

            if (state.FileAssociationBackups.Any(b => b.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                return;

            state.FileAssociationBackups.Add(new FileAssociationBackup
            {
                Extension = extension,
                OriginalMimeType = mimeType,
                OriginalDefaultApp = string.IsNullOrEmpty(originalDefault) ? null : originalDefault,
            });
            InstallStateIo.WriteState(installDir, state);
        }
        catch { }
    }

    private static FileAssociationBackup? GetBackup(string extension)
    {
        var installDir = InstallBootstrap.InstallDirectory;
        if (string.IsNullOrEmpty(installDir)) return null;

        try
        {
            var state = InstallStateIo.ReadState(installDir);
            return state.FileAssociationBackups?.FirstOrDefault(b =>
                b.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetCreatedFiles(FileAssociationInfo association, string mimeType)
    {
        var files = new List<string>();

        var mimeXmlPath = Path.Combine(GetMimePackagesPath(),
            $"{association.ProgId.Replace('.', '-')}-{association.Extension.TrimStart('.')}.xml");
        if (File.Exists(mimeXmlPath))
            files.Add(mimeXmlPath);

        var backup = GetBackup(association.Extension);
        if (backup?.BackupFilePaths is not null)
            files.AddRange(backup.BackupFilePaths);

        return files.Distinct().ToList();
    }

    private static string GetMimeDatabasePath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "mime");

    private static string GetMimePackagesPath()
        => Path.Combine(GetMimeDatabasePath(), "packages");

    private static string GetApplicationsPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "applications");

    private static void RunCommand(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            psi.ArgumentList.Add(arg);
        using var p = Process.Start(psi);
        p?.WaitForExit();
    }
}
