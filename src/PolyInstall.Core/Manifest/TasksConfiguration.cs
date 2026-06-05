using System.ComponentModel;

namespace PolyInstall.Manifest;

public sealed class TasksConfiguration
{
    [Description(
        "Tasks that run after the user confirms the install destination, before any files are copied to disk.")]
    public List<InstallTask>? PreInstall { get; set; }

    [Description(
        "Tasks that run after all files have been copied to the install directory and the installation is complete.")]
    public List<InstallTask>? PostInstall { get; set; }

    [Description(
        "Tasks that run at the very start of uninstall, before the Add/Remove Programs entry is removed and before any installed files are deleted from disk. " +
        "Installed files are still fully accessible here.")]
    public List<InstallTask>? PreUninstall { get; set; }

    [Description(
        "Tasks that run after pre_uninstall tasks — but STILL BEFORE files are deleted from disk and BEFORE the " +
        "Add/Remove Programs entry is removed. Despite its name, post_uninstall does not execute after the " +
        "installation has been cleaned up. Both pre_uninstall and post_uninstall run while installed files are " +
        "still present. Use post_uninstall for tasks that must follow pre_uninstall sequentially (e.g., stopping " +
        "a service before removing its config) rather than for tasks that need the install tree to be gone.")]
    public List<InstallTask>? PostUninstall { get; set; }
}

public sealed class InstallTask
{
    [Description(
        "Optional OS predicate that must be true for this task to run. " +
        "Supported values: os.isWindows/os.is_windows, os.isLinux/os.is_linux, " +
        "os.isOSX/os.is_osx, os.isMacOS/os.is_macos, os.isUnix/os.is_unix. " +
        "If omitted the task always runs.")]
    public string? Require { get; set; }

    [Description(
        "The action to perform. One of: create_shortcut, write_registry (Windows only), " +
        "create_desktop_entry (Linux/macOS only), set_permissions (Unix only), add_to_path.")]
    public string Action { get; set; } = "";

    [Description("Key/value map of parameters for the chosen action. Keys are snake_case strings.")]
    public Dictionary<string, object?>? Parameters { get; set; }
}
