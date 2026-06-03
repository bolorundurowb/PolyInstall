using System.Diagnostics;
using PolyInstall.Build;
using PolyInstall.Manifest;

namespace PolyInstall.Cli.Build;

public static class AppImagePackager
{
    private static readonly HttpClient Http = new();

    /// <summary>Official AppImage type-2 runtime blobs (MIT, AppImageKit).</summary>
    private static readonly IReadOnlyDictionary<string, string> RidToRuntimeUrl = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["linux-x64"] = "https://github.com/AppImage/AppImageKit/releases/download/12/runtime-x86_64",
        ["linux-arm64"] = "https://github.com/AppImage/AppImageKit/releases/download/12/runtime-aarch64",
    };

    public static async Task CreateAsync(
        string bundleElfPath,
        InstallManifest manifest,
        string manifestTargetToken,
        string safeBaseName,
        string outputDirectory,
        string workspaceBaseDirectory,
        CancellationToken ct)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new InvalidOperationException(
                "AppImage packaging must run on Linux: mksquashfs (squashfs-tools) is required. Build on a Linux host or CI agent.");
        }

        BuildLog.Info("AppImage: resolving mksquashfs…");
        var mksquashfs = FindOnPath("mksquashfs");
        if (mksquashfs is null)
            throw new InvalidOperationException("mksquashfs not found on PATH. Install squashfs-tools (e.g. apt install squashfs-tools).");
        BuildLog.VerboseLine($"AppImage: mksquashfs at {mksquashfs}");

        var rid = RidMapping.ToDotNetRid(manifestTargetToken);
        if (!RidToRuntimeUrl.TryGetValue(rid, out var runtimeUrl))
            throw new InvalidOperationException($"No AppImage runtime URL mapped for RID '{rid}'.");
        BuildLog.VerboseLine($"AppImage: runtime URL for {rid}: {runtimeUrl}");

        var binName = Path.GetFileName(bundleElfPath);
        var appDir = Path.Combine(Path.GetTempPath(), "polyinstall-appdir-" + Guid.NewGuid().ToString("n"));
        var squashfs = Path.Combine(Path.GetTempPath(), "polyinstall-squash-" + Guid.NewGuid().ToString("n") + ".squashfs");
        var outName = $"{safeBaseName}-{manifestTargetToken}.AppImage";
        var outPath = Path.Combine(outputDirectory, outName);
        try
        {
            BuildLog.Info($"AppImage: staging AppDir at {appDir}");
            var usrBin = Path.Combine(appDir, "usr", "bin");
            Directory.CreateDirectory(usrBin);
            File.Copy(bundleElfPath, Path.Combine(usrBin, binName), overwrite: true);
            ChmodExec(Path.Combine(usrBin, binName));

            var desktopName = $"{safeBaseName}.desktop";
            var desktopPath = Path.Combine(appDir, desktopName);
            var iconName = TryCopyIcon(manifest, workspaceBaseDirectory, appDir);
            var desktopLines = new List<string>
            {
                "[Desktop Entry]",
                "Type=Application",
                $"Name={manifest.Metadata.Name}",
                "Exec=AppRun",
            };
            if (!string.IsNullOrEmpty(iconName))
                desktopLines.Add($"Icon={iconName}");
            desktopLines.Add("Terminal=false");
            desktopLines.Add("Categories=Utility;");
            await File.WriteAllTextAsync(desktopPath, string.Join(Environment.NewLine, desktopLines) + Environment.NewLine, ct);
            Chmod644(desktopPath);

            var appRunPath = Path.Combine(appDir, "AppRun");
            await File.WriteAllTextAsync(
                appRunPath,
                $"""
                #!/bin/sh
                HERE="$(dirname "$(readlink -f "$0" 2>/dev/null || echo "$0")")"
                exec "$HERE/usr/bin/{binName}" "$@"
                """,
                ct);
            ChmodExec(appRunPath);

            BuildLog.Info("AppImage: creating squashfs image…");
            RunMksquashfs(mksquashfs, appDir, squashfs, ct);

            BuildLog.Info("AppImage: assembling type-2 image (runtime + squashfs)…");
            await using var outFs = File.Create(outPath);
            await using (var runtimeFs = await OpenRuntimeAsync(runtimeUrl, ct))
                await runtimeFs.CopyToAsync(outFs, ct);
            await using (var sq = File.OpenRead(squashfs))
                await sq.CopyToAsync(outFs, ct);

            ChmodExec(outPath);

            BuildLog.Info($"Built AppImage {outPath} ({BuildLog.FormatBytes(new FileInfo(outPath).Length)})");
        }
        finally
        {
            TryDelete(appDir);
            TryDelete(squashfs);
        }
    }

    private static string? TryCopyIcon(InstallManifest manifest, string workspaceBase, string appDir)
    {
        try
        {
            var asset = manifest.Ui.Assets?.FirstOrDefault(a =>
                a.Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
            if (asset is null)
                return null;

            var src = Path.IsPathRooted(asset.Path)
                ? asset.Path
                : Path.GetFullPath(Path.Combine(workspaceBase, asset.Path));
            if (!File.Exists(src))
                return null;

            var iconDir = Path.Combine(appDir, "usr", "share", "icons", "hicolor", "256x256", "apps");
            Directory.CreateDirectory(iconDir);
            const string iconFile = "appicon.png";
            File.Copy(src, Path.Combine(iconDir, iconFile), overwrite: true);
            return iconFile;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<Stream> OpenRuntimeAsync(string url, CancellationToken ct)
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "polyinstall-appimage-runtime-cache");
        Directory.CreateDirectory(cacheDir);
        var cacheFile = Path.Combine(cacheDir, SanitizeFileName(url));
        if (!File.Exists(cacheFile))
        {
            await using var s = await Http.GetStreamAsync(url, ct);
            await using var fs = File.Create(cacheFile);
            await s.CopyToAsync(fs, ct);
        }

        return File.OpenRead(cacheFile);
    }

    private static string SanitizeFileName(string url)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            url = url.Replace(c, '_');
        return url.Trim('_');
    }

    private static string? FindOnPath(string fileName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = Path.Combine(dir, fileName);
            if (File.Exists(p))
                return p;
        }

        return null;
    }

    private static void RunMksquashfs(string mksquashfs, string appDir, string squashfsOut, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = mksquashfs,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        psi.ArgumentList.Add(appDir);
        psi.ArgumentList.Add(squashfsOut);
        psi.ArgumentList.Add("-comp");
        psi.ArgumentList.Add("xz");
        psi.ArgumentList.Add("-noappend");
        psi.ArgumentList.Add("-no-xattrs");
        psi.ArgumentList.Add("-no-fragments");

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {mksquashfs}.");
        p.WaitForExit();
        ct.ThrowIfCancellationRequested();
        if (p.ExitCode != 0)
        {
            var err = p.StandardError.ReadToEnd();
            var o = p.StandardOutput.ReadToEnd();
            throw new InvalidOperationException($"{mksquashfs} failed ({p.ExitCode}): {err}\n{o}");
        }
    }

    private static void ChmodExec(string path)
    {
        var psi = new ProcessStartInfo { FileName = "chmod", UseShellExecute = false };
        psi.ArgumentList.Add("+x");
        psi.ArgumentList.Add(path);
        using var p = Process.Start(psi);
        p?.WaitForExit();
    }

    private static void Chmod644(string path)
    {
        var psi = new ProcessStartInfo { FileName = "chmod", UseShellExecute = false };
        psi.ArgumentList.Add("644");
        psi.ArgumentList.Add(path);
        using var p = Process.Start(psi);
        p?.WaitForExit();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
            else if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
