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
        string runningExePath,
        string? expectedInstallRoot = null)
    {
        var installRoot = ValidateAndResolveInstallRoot(state, manifest, expectedInstallRoot);

        InstallBootstrap.Init(manifest, installRoot, pal);
        InstallBootstrap.InstallDirectory = installRoot;

        var installedFeatures = ResolveInstalledFeatures(manifest, state);
        InstallBootstrap.SelectedFeatures = new HashSet<string>(installedFeatures, StringComparer.OrdinalIgnoreCase);

        TaskEngine.RunPhase(manifest.Tasks?.PreUninstall, pal, installedFeatures, isUninstall: true);
        TaskEngine.RunPhase(manifest.Tasks?.PostUninstall, pal, installedFeatures, isUninstall: true);

        if (manifest.FileAssociations is { Count: > 0 } && pal.FileAssociations is not null)
        {
            foreach (var assoc in manifest.FileAssociations)
            {
                if (!FeatureFilter.IsActive(assoc.Features, installedFeatures))
                    continue;
                var info = InstallCoordinator.MapToFileAssociationInfo(assoc, pal);
                pal.FileAssociations.Unregister(info);
            }
        }

        RemoveRegisteredServices(state.RegisteredServices, pal);

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

        DeleteAllFilesExcept(runningExePath, installRoot);

        if (OperatingSystem.IsWindows())
            ScheduleWindowsDeleteInstallRoot(installRoot);
        else
            TryDeleteDirectoryRecursive(installRoot);
    }

    private static string ValidateAndResolveInstallRoot(
        InstallStateDocument state,
        InstallManifest manifest,
        string? expectedInstallRoot)
    {
        if (string.IsNullOrWhiteSpace(state.InstallLocation))
            throw new InvalidOperationException("Install state does not contain an install location.");

        var installRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(state.InstallLocation));

        if (!string.IsNullOrWhiteSpace(expectedInstallRoot)
            && !SamePath(installRoot, expectedInstallRoot))
        {
            throw new InvalidOperationException(
                "Install state location does not match the requested install directory.");
        }

        var expectedProductId = ProductIdHelper.StableProductGuidString(manifest.Metadata);
        if (!state.ProductId.Equals(expectedProductId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Install state product id does not match the embedded manifest.");
        }

        if (IsDangerousInstallRoot(installRoot))
        {
            throw new InvalidOperationException(
                $"Refusing to uninstall from unsafe install root: {installRoot}");
        }

        return installRoot;
    }

    private static bool SamePath(string left, string right)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(left))
                .Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return left.Equals(right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static bool IsDangerousInstallRoot(string installRoot)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));
        var volumeRoot = Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root) ?? "");
        if (!string.IsNullOrEmpty(volumeRoot)
            && root.Equals(volumeRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return DangerousRoots()
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
            .Any(path => root.Equals(path, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> DangerousRoots()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.System);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    }

    /// <summary>
    /// Resolves which features were installed. Prefers state.SelectedFeatures (recorded by
    /// the installer). Falls back to all manifest features for legacy installs that pre-date
    /// feature support so uninstall remains complete and backward compatible.
    /// </summary>
    private static IReadOnlySet<string> ResolveInstalledFeatures(InstallManifest manifest, InstallStateDocument state)
    {
        if (state.SelectedFeatures is { Count: > 0 } recorded)
            return new HashSet<string>(recorded, StringComparer.OrdinalIgnoreCase);

        var fallback = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (manifest.Features is { Count: > 0 } features)
        {
            foreach (var feat in features)
            {
                if (!string.IsNullOrWhiteSpace(feat.Id))
                    fallback.Add(feat.Id);
            }
        }
        return fallback;
    }

    private static void RemoveRegisteredServices(IReadOnlyCollection<RegisteredServiceInfo>? services, IPolyInstallPal pal)
    {
        if (services is not { Count: > 0 })
            return;
        if (pal.Services is null)
            throw new PlatformNotSupportedException("Service management is not supported on this platform.");

        var platform = CurrentServicePlatform();
        foreach (var service in services)
        {
            if (!service.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))
                continue;

            pal.Services.Remove(service);
        }
    }

    private static string CurrentServicePlatform() =>
        OperatingSystem.IsWindows()
            ? "windows"
            : OperatingSystem.IsMacOS()
                ? "macos"
                : "linux";

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
