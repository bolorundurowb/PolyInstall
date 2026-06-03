namespace PolyInstall.Install;

/// <summary>
/// Relative paths under the install directory for PolyInstall metadata, embedded JSON, and the Windows uninstall layout
/// (<c>Uninstall.exe</c> at the root and <c>PolyInstall.Uninstall.exe</c> under <c>.polyinstall/tools</c>).
/// </summary>
public static class InstallStatePaths
{
    public const string PolyDirName = ".polyinstall";
    public const string ToolsDirName = "tools";
    public const string InstallStateFileName = "install-state.json";
    public const string EmbeddedManifestFileName = "embedded-manifest.json";
    public const string UninstallExeFileName = "Uninstall.exe";
    public const string UninstallPayloadFileName = "PolyInstall.Uninstall.exe";

    public static string PolyDir(string installRoot) => Path.Combine(installRoot, PolyDirName);
    public static string ToolsDir(string installRoot) => Path.Combine(PolyDir(installRoot), ToolsDirName);

    public static string InstallStatePath(string installRoot) =>
        Path.Combine(PolyDir(installRoot), InstallStateFileName);

    public static string EmbeddedManifestPath(string installRoot) =>
        Path.Combine(PolyDir(installRoot), EmbeddedManifestFileName);

    public static string UninstallExePath(string installRoot) =>
        Path.Combine(installRoot, UninstallExeFileName);

    public static string UninstallPayloadPath(string installRoot) =>
        Path.Combine(ToolsDir(installRoot), UninstallPayloadFileName);
}
