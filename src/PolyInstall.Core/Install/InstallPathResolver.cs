using PolyInstall.Core.Hosting;

namespace PolyInstall.Core.Install;

/// <summary>
/// Maps manifest placeholders to OS paths via a small PAL surface.
/// </summary>
public static class InstallPathResolver
{
    public static string Expand(string path, IInstallPathPal pal)
    {
        var appDir = InstallBootstrap.InstallDirectory ?? InstallBootstrap.ExtractRoot;
        return path
            .Replace("{AppDir}", appDir, StringComparison.OrdinalIgnoreCase)
            .Replace("{ProgramFiles}", pal.ProgramFiles, StringComparison.OrdinalIgnoreCase)
            .Replace("{UserHome}", pal.UserHome, StringComparison.OrdinalIgnoreCase)
            .Replace("{Desktop}", pal.Desktop, StringComparison.OrdinalIgnoreCase);
    }
}

public interface IInstallPathPal
{
    string AppDir { get; }
    string ProgramFiles { get; }
    string UserHome { get; }
    string Desktop { get; }
}
