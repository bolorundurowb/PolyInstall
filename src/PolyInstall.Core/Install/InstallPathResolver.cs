using PolyInstall.Core.Hosting;

namespace PolyInstall.Core.Install;

/// <summary>
/// Maps manifest placeholders to OS paths via a small PAL surface.
/// </summary>
public static class InstallPathResolver
{
    public static string Expand(string path, IInstallPathPal pal)
        => Expand(path, pal, GetTargetOsFromInstallerOrHost());

    public static string Expand(string path, IInstallPathPal pal, TargetOperatingSystem targetOs)
    {
        var appDir = !string.IsNullOrEmpty(pal.AppDir)
            ? pal.AppDir
            : InstallBootstrap.InstallDirectory ?? InstallBootstrap.ExtractRoot;
        var expanded = path
            .Replace("{AppDir}", appDir, StringComparison.OrdinalIgnoreCase)
            .Replace("{ProgramFiles}", pal.ProgramFiles, StringComparison.OrdinalIgnoreCase)
            .Replace("{UserHome}", pal.UserHome, StringComparison.OrdinalIgnoreCase)
            .Replace("{Desktop}", pal.Desktop, StringComparison.OrdinalIgnoreCase);

        return NormalizeDirectorySeparators(expanded, GetDirectorySeparator(targetOs));
    }

    private static TargetOperatingSystem GetTargetOsFromInstallerOrHost()
    {
        var installerTarget = InstallBootstrap.Manifest?.Build?.InstallerTarget;
        if (TryParseInstallerTargetOperatingSystem(installerTarget, out var targetOs))
            return targetOs;

        return GetCurrentHostOs();
    }

    /// <summary>
    /// Maps a build <c>installer_target</c> RID token (for example <c>win-x64</c>, <c>linux-arm64</c>, <c>osx-arm64</c>)
    /// to the OS family used for path normalization. Returns <c>false</c> when the token is missing or unrecognized.
    /// </summary>
    public static bool TryParseInstallerTargetOperatingSystem(string? targetToken, out TargetOperatingSystem targetOs)
        => TryParseTargetOs(targetToken, out targetOs);

    private static bool TryParseTargetOs(string? targetToken, out TargetOperatingSystem targetOs)
    {
        targetOs = default;
        if (string.IsNullOrWhiteSpace(targetToken))
            return false;

        var token = targetToken.Trim().ToLowerInvariant();
        if (token.StartsWith("windows-", StringComparison.Ordinal) || token.StartsWith("win-", StringComparison.Ordinal))
        {
            targetOs = TargetOperatingSystem.Windows;
            return true;
        }

        if (token.StartsWith("linux-", StringComparison.Ordinal))
        {
            targetOs = TargetOperatingSystem.Linux;
            return true;
        }

        if (token.StartsWith("osx-", StringComparison.Ordinal) || token.StartsWith("macos-", StringComparison.Ordinal))
        {
            targetOs = TargetOperatingSystem.MacOs;
            return true;
        }

        return false;
    }

    private static TargetOperatingSystem GetCurrentHostOs()
    {
        if (OperatingSystem.IsWindows())
            return TargetOperatingSystem.Windows;
        if (OperatingSystem.IsMacOS())
            return TargetOperatingSystem.MacOs;
        return TargetOperatingSystem.Linux;
    }

    private static char GetDirectorySeparator(TargetOperatingSystem targetOs)
    {
        return targetOs == TargetOperatingSystem.Windows ? '\\' : '/';
    }

    private static string NormalizeDirectorySeparators(string path, char directorySeparator)
    {
        return directorySeparator == '\\'
            ? path.Replace('/', '\\')
            : path.Replace('\\', '/');
    }
}

public enum TargetOperatingSystem
{
    Windows,
    Linux,
    MacOs,
}

public interface IInstallPathPal
{
    string AppDir { get; }
    string ProgramFiles { get; }
    string UserHome { get; }
    string Desktop { get; }
}
