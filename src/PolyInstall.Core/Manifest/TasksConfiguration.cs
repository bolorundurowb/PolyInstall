using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Defines the configuration for installation and uninstallation tasks.
/// </summary>
public sealed class TasksConfiguration
{
    /// <summary>
    /// Gets or sets the tasks that run after the user confirms the installation destination,
    /// but before any files are copied to disk.
    /// </summary>
    [Description("Tasks that run after the user confirms the installation destination, but before any files are copied to disk.")]
    public List<InstallTask>? PreInstall { get; set; }

    /// <summary>
    /// Gets or sets the tasks that run after all files have been copied to the installation
    /// directory and the installation is complete.
    /// </summary>
    [Description("Tasks that run after all files have been copied to the installation directory and the installation is complete.")]
    public List<InstallTask>? PostInstall { get; set; }

    /// <summary>
    /// Gets or sets the tasks that run at the very start of uninstallation, before the
    /// Add/Remove Programs entry is removed and before any installed files are deleted.
    /// </summary>
    [Description("Tasks that run at the very start of uninstallation, before files are deleted.")]
    public List<InstallTask>? PreUninstall { get; set; }

    /// <summary>
    /// Gets or sets the tasks that run after pre-uninstallation tasks, but still before
    /// files are deleted and before the Add/Remove Programs entry is removed.
    /// Use this for tasks that must follow pre-uninstallation sequentially.
    /// </summary>
    [Description("Tasks that run after pre-uninstallation tasks, but still before files are deleted.")]
    public List<InstallTask>? PostUninstall { get; set; }
}

/// <summary>
/// Defines a single installation or uninstallation task.
/// </summary>
public sealed class InstallTask
{
    /// <summary>
    /// Gets or sets the optional OS predicate that must be true for this task to run.
    /// Supported values: <c>os.isWindows</c>, <c>os.isLinux</c>, <c>os.isMacOS</c>, <c>os.isUnix</c>, etc.
    /// </summary>
    [Description("Optional OS predicate that must be true for this task to run. Supported values: os.isWindows, os.isLinux, os.isMacOS, os.isUnix, etc.")]
    public string? Require { get; set; }

    /// <summary>
    /// Gets or sets the action to perform.
    /// Supported actions: <c>create_shortcut</c>, <c>write_registry</c>, <c>create_desktop_entry</c>,
    /// <c>set_permissions</c>, <c>add_to_path</c>, <c>file_association</c>.
    /// </summary>
    [Description("Action to perform. Supported actions: create_shortcut, write_registry, create_desktop_entry, set_permissions, add_to_path, file_association.")]
    public string Action { get; set; } = "";

    /// <summary>Gets or sets the key/value map of parameters for the chosen action.</summary>
    [Description("Key/value map of parameters for the chosen action.")]
    public Dictionary<string, object?>? Parameters { get; set; }

    /// <summary>
    /// Gets or sets the optional list of feature identifiers that gate this task.
    /// When null or empty, the task always runs (subject to the <c>Require</c> predicate).
    /// </summary>
    [Description("Optional list of feature identifiers that gate this task. When null or empty, the task always runs (subject to the Require predicate).")]
    public List<string>? Features { get; set; }
}
