using PolyInstall.Core.Manifest;

namespace PolyInstall.Core.Install;

public static class DefaultInstallPathResolver
{
    public static string GetDefaultInstallPath(InstallManifest manifest, IInstallPathPal pal)
    {
        var productName = manifest.Metadata.Name;
        if (!OperatingSystem.IsWindows())
            return Path.Combine(pal.ProgramFiles, productName);

        if (InstallScopeHelper.IsMachineInstall(manifest))
            return Path.Combine(pal.ProgramFiles, productName);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData) ? pal.UserHome : localAppData;
        return Path.Combine(baseDirectory, productName);
    }
}
