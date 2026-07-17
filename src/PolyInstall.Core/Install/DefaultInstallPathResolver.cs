using PolyInstall.Manifest;

namespace PolyInstall.Install;

public static class DefaultInstallPathResolver
{
    public static string GetDefaultInstallPath(InstallManifest manifest, IInstallPathPal pal)
    {
        var productName = manifest.Metadata.Name;
        if (!OperatingSystem.IsWindows())
            return Path.Combine(pal.ProgramFiles, productName);

        if (InstallScopeHelper.IsMachineInstall(manifest))
            return Path.Combine(pal.ProgramFiles, productName);

        return Path.Combine(pal.LocalAppData, productName);
    }
}
