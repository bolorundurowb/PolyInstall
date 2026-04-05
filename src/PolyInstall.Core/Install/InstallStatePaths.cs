namespace PolyInstall.Core.Install;

public static class InstallStatePaths
{
    public const string PolyDirName = ".polyinstall";
    public const string InstallStateFileName = "install-state.json";
    public const string EmbeddedManifestFileName = "embedded-manifest.json";
    public const string UninstallExeFileName = "Uninstall.exe";

    public static string PolyDir(string installRoot) => Path.Combine(installRoot, PolyDirName);

    public static string InstallStatePath(string installRoot) =>
        Path.Combine(PolyDir(installRoot), InstallStateFileName);

    public static string EmbeddedManifestPath(string installRoot) =>
        Path.Combine(PolyDir(installRoot), EmbeddedManifestFileName);

    public static string UninstallExePath(string installRoot) =>
        Path.Combine(installRoot, UninstallExeFileName);
}
