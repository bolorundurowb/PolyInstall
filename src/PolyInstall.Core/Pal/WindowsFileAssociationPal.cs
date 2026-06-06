using System.Runtime.Versioning;
using Microsoft.Win32;
using PolyInstall.Hosting;
using PolyInstall.Install;

namespace PolyInstall.Pal;

internal sealed class WindowsFileAssociationPal : IFileAssociationPal
{
    [SupportedOSPlatform("windows")]
    public void Register(FileAssociationInfo association)
    {
        // 1. Back up existing association if it exists
        Backup(association.Extension);

        // 2. Register the extension
        using (var key = Registry.ClassesRoot.CreateSubKey(association.Extension))
        {
            key.SetValue("", association.ProgId);
        }

        // 3. Register the ProgID
        using (var key = Registry.ClassesRoot.CreateSubKey(association.ProgId))
        {
            key.SetValue("", association.Description);

            if (!string.IsNullOrEmpty(association.Icon))
            {
                using var iconKey = key.CreateSubKey("DefaultIcon");
                iconKey.SetValue("", association.Icon);
            }

            using var shellKey = key.CreateSubKey("shell");
            using var openKey = shellKey.CreateSubKey("open");
            using var commandKey = openKey.CreateSubKey("command");
            commandKey.SetValue("", association.Command);
        }
    }

    [SupportedOSPlatform("windows")]
    public void Unregister(FileAssociationInfo association)
    {
        // 1. Delete the ProgID key
        Registry.ClassesRoot.DeleteSubKeyTree(association.ProgId, false);

        // 2. Restore or delete the extension key
        var backup = GetBackup(association.Extension);
        if (backup != null && !string.IsNullOrEmpty(backup.OriginalProgId))
        {
            using var key = Registry.ClassesRoot.CreateSubKey(association.Extension);
            key.SetValue("", backup.OriginalProgId);
        }
        else
        {
            Registry.ClassesRoot.DeleteSubKeyTree(association.Extension, false);
        }
    }

    [SupportedOSPlatform("windows")]
    private void Backup(string extension)
    {
        var installDir = InstallBootstrap.InstallDirectory;
        if (string.IsNullOrEmpty(installDir)) return;

        string? originalProgId = null;
        using (var key = Registry.ClassesRoot.OpenSubKey(extension))
        {
            if (key != null)
            {
                originalProgId = key.GetValue("") as string;
            }
        }

        try
        {
            var state = InstallStateIo.ReadState(installDir);
            state.FileAssociationBackups ??= new List<FileAssociationBackup>();

            if (state.FileAssociationBackups.Any(b => b.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                return; // Already backed up

            state.FileAssociationBackups.Add(new FileAssociationBackup
            {
                Extension = extension,
                OriginalProgId = originalProgId
            });
            InstallStateIo.WriteState(installDir, state);
        }
        catch
        {
            // If we can't read/write state, we just don't back up.
        }
    }

    private FileAssociationBackup? GetBackup(string extension)
    {
        var installDir = InstallBootstrap.InstallDirectory;
        if (string.IsNullOrEmpty(installDir)) return null;

        try
        {
            var state = InstallStateIo.ReadState(installDir);
            return state.FileAssociationBackups?.FirstOrDefault(b => b.Extension.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }
}
