using System.Diagnostics;
using PolyInstall.Core.Hosting;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;

namespace PolyInstall.Core.Install;

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

        TaskEngine.RunPhase(manifest.Tasks?.PreUninstall, pal);
        TaskEngine.RunPhase(manifest.Tasks?.PostUninstall, pal);

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
            // best-effort
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
            // best-effort
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
            // best-effort
        }
    }

    private static void ScheduleWindowsDeleteInstallRoot(string installRoot)
    {
        installRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));

        // Run detached and retry because Uninstall.exe is still running while this method executes.
        var args = $"/c for /l %i in (1,1,15) do @rmdir /s /q \"{installRoot}\" && exit /b 0 || timeout /t 1 /nobreak >nul";
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}
