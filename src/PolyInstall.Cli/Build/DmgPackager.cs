using System.Diagnostics;

namespace PolyInstall.Cli.Build;

public static class DmgPackager
{
    public static void Create(string bundleMachOPath, string outputDmgPath, string volumeLabel)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new InvalidOperationException(
                "DMG packaging requires macOS with hdiutil. Build on a Mac host or macOS CI agent.");
        }

        var stage = Path.Combine(Path.GetTempPath(), "polyinstall-dmg-" + Guid.NewGuid().ToString("n"));
        try
        {
            BuildLog.Info($"DMG: staging bundle at {stage}");
            Directory.CreateDirectory(stage);
            var name = Path.GetFileName(bundleMachOPath);
            File.Copy(bundleMachOPath, Path.Combine(stage, name), overwrite: true);

            RunLnSymlink(Path.Combine(stage, "Applications"));

            var psi = new ProcessStartInfo
            {
                FileName = "hdiutil",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            psi.ArgumentList.Add("create");
            psi.ArgumentList.Add("-volname");
            psi.ArgumentList.Add(volumeLabel);
            psi.ArgumentList.Add("-srcfolder");
            psi.ArgumentList.Add(stage);
            psi.ArgumentList.Add("-format");
            psi.ArgumentList.Add("UDZO");
            psi.ArgumentList.Add("-ov");
            psi.ArgumentList.Add(outputDmgPath);

            BuildLog.Info($"DMG: running hdiutil create → {outputDmgPath}");
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not start hdiutil.");
            p.WaitForExit();
            if (p.ExitCode != 0)
            {
                var err = p.StandardError.ReadToEnd();
                var o = p.StandardOutput.ReadToEnd();
                throw new InvalidOperationException($"hdiutil failed ({p.ExitCode}): {err}\n{o}");
            }

            BuildLog.Info($"Built DMG {outputDmgPath} ({BuildLog.FormatBytes(new FileInfo(outputDmgPath).Length)})");
        }
        finally
        {
            TryDeleteDir(stage);
        }
    }

    private static void RunLnSymlink(string applicationsLinkPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "ln",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        psi.ArgumentList.Add("-s");
        psi.ArgumentList.Add("/Applications");
        psi.ArgumentList.Add(applicationsLinkPath);

        using var p = Process.Start(psi);
        p?.WaitForExit();
    }

    private static void TryDeleteDir(string path)
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
}
