using System.Diagnostics;
using System.Text;
using PolyInstall.Hosting;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Install;

public static class UninstallCoordinator
{
    /// <summary>
    /// Runs uninstall: tasks, ARP removal, file deletion, then schedules removal of the install root (including this process when it lives under the root).
    /// </summary>
    public static void Run(
        InstallStateDocument state,
        InstallManifest manifest,
        IPolyInstallPal pal,
        string runningExePath)
    {
        InstallBootstrap.Init(manifest, state.InstallLocation, pal);
        InstallBootstrap.InstallDirectory = state.InstallLocation;

        TaskEngine.RunPhase(manifest.Tasks?.PreUninstall, pal, isUninstall: true);
        TaskEngine.RunPhase(manifest.Tasks?.PostUninstall, pal, isUninstall: true);

        if (state.AddedToPath is { Count: > 0 } && pal.Path is not null)
        {
            var scope = state.InstallScope.Equals("machine", StringComparison.OrdinalIgnoreCase)
                ? "machine"
                : "user";
            foreach (var pathEntry in state.AddedToPath)
                pal.Path.RemoveFromPath(pathEntry, scope);
        }

        if (OperatingSystem.IsWindows())
            WindowsArpRegistration.Unregister(state);

        DeleteAllFilesExcept(runningExePath, state.InstallLocation);

        if (OperatingSystem.IsWindows())
            ScheduleWindowsDeleteInstallRoot(state.InstallLocation);
        else
            TryDeleteDirectoryRecursive(state.InstallLocation);
    }

    private static void DeleteAllFilesExcept(string runningExePath, string installRoot)
    {
        installRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));
        runningExePath = Path.GetFullPath(runningExePath);

        if (!Directory.Exists(installRoot))
            return;

        foreach (var file in Directory.EnumerateFiles(installRoot, "*", SearchOption.AllDirectories))
        {
            if (string.Equals(file, runningExePath, StringComparison.OrdinalIgnoreCase))
                continue;
            TryDeleteFile(file);
        }

        foreach (var dir in Directory.EnumerateDirectories(installRoot, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static d => d.Length))
        {
            TryDeleteEmptyDirectory(dir);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).FirstOrDefault() is null)
                Directory.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectoryRecursive(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }

    private static void ScheduleWindowsDeleteInstallRoot(string installRoot)
    {
        installRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));

        // Uninstall.exe may still be running from installRoot; schedule a detached cleanup with retries.
        var escapedPath = installRoot.Replace("'", "''", StringComparison.Ordinal);
        var script =
            $"$d = '{escapedPath}'; " +
            "for ($i = 0; $i -lt 15; $i++) { " +
            "  if (-not (Test-Path -LiteralPath $d)) { exit 0 } " +
            "  try { Remove-Item -LiteralPath $d -Recurse -Force -ErrorAction Stop; exit 0 } " +
            "  catch { Start-Sleep -Seconds 1 } " +
            "} " +
            "Remove-Item -LiteralPath $d -Recurse -Force";
        var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {enc}",
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}
