using System.Globalization;
using System.Text.Json;
using PolyInstall.Conditions;
using PolyInstall.Hosting;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Install;

public static class TaskEngine
{
    public static void RunPhase(IEnumerable<InstallTask>? tasks, IPolyInstallPal pal, bool isUninstall = false)
        => RunPhase(tasks, pal, selectedFeatures: null, isUninstall);

    /// <summary>
    /// Runs the given task list, skipping tasks whose <see cref="InstallTask.Features"/> list
    /// has no intersection with <paramref name="selectedFeatures"/>. Tasks without a Features
    /// list always run (subject to <see cref="InstallTask.Require"/>).
    /// </summary>
    public static void RunPhase(
        IEnumerable<InstallTask>? tasks,
        IPolyInstallPal pal,
        IReadOnlySet<string>? selectedFeatures,
        bool isUninstall = false)
    {
        if (tasks is null)
            return;
        var features = selectedFeatures ?? InstallBootstrap.SelectedFeatures;
        foreach (var task in tasks)
        {
            if (!ConditionEvaluator.Evaluate(task.Require))
                continue;
            if (!FeatureFilter.IsActive(task.Features, features))
                continue;
            Dispatch(task, pal, isUninstall);
        }
    }

    private static void Dispatch(InstallTask task, IPolyInstallPal pal, bool isUninstall)
    {
        var action = task.Action.Trim().ToLowerInvariant();
        var p = task.Parameters ?? new Dictionary<string, object?>();

        switch (action)
        {
            case "create_shortcut":
                if (isUninstall)
                    throw new InvalidOperationException($"Task action '{task.Action}' is not valid in uninstall phase. Remove it from pre_uninstall/post_uninstall.");
                var shortcutPath = BuildShortcutPath(
                    pal,
                    GetString(p, "location"),
                    GetOptionalString(p, "subfolder"),
                    GetString(p, "name"));
                pal.Shortcuts.CreateFileShortcut(
                    Expand(GetString(p, "target_path"), pal),
                    shortcutPath,
                    ExpandOptional(GetOptionalString(p, "description"), pal),
                    ExpandOptional(GetOptionalString(p, "icon_path"), pal));
                break;
            case "write_registry":
                if (isUninstall)
                    throw new InvalidOperationException($"Task action '{task.Action}' is not valid in uninstall phase. Remove it from pre_uninstall/post_uninstall.");
                if (pal.Registry is null)
                    throw new PlatformNotSupportedException("Registry tasks are not supported on this platform.");
                pal.Registry.SetValue(
                    GetString(p, "key_path"),
                    GetOptionalString(p, "value_name"),
                    Expand(GetString(p, "value"), pal),
                    GetString(p, "value_kind"));
                break;
            case "create_desktop_entry":
                if (isUninstall)
                    throw new InvalidOperationException($"Task action '{task.Action}' is not valid in uninstall phase. Remove it from pre_uninstall/post_uninstall.");
                if (pal.DesktopEntries is null)
                    throw new PlatformNotSupportedException("Desktop entry tasks are not supported on this platform.");
                pal.DesktopEntries.CreateDesktopEntry(
                    Expand(GetString(p, "file_name"), pal),
                    Expand(GetString(p, "name"), pal),
                    Expand(GetString(p, "exec"), pal),
                    ExpandOptional(GetOptionalString(p, "icon"), pal),
                    ExpandOptional(GetOptionalString(p, "comment"), pal));
                break;
            case "set_permissions":
                if (isUninstall)
                    throw new InvalidOperationException($"Task action '{task.Action}' is not valid in uninstall phase. Remove it from pre_uninstall/post_uninstall.");
                if (pal.FilePermissions is null)
                    throw new PlatformNotSupportedException("Permission tasks are not supported on this platform.");
                pal.FilePermissions.SetFileMode(Expand(GetString(p, "path"), pal), GetOctalMode(p, "mode"));
                break;
            case "add_to_path":
                if (isUninstall)
                    throw new InvalidOperationException($"Task action '{task.Action}' is not valid in uninstall phase. Remove it from pre_uninstall/post_uninstall.");
                if (pal.Path is null)
                    throw new PlatformNotSupportedException("PATH tasks are not supported on this platform.");
                var pathValue = Expand(GetStringOrDefault(p, "path", InstallBootstrap.InstallDirectory ?? ""), pal);
                var pathScope = GetOptionalString(p, "scope") ?? "user";
                if (!pathScope.Equals("user", StringComparison.OrdinalIgnoreCase)
                    && !pathScope.Equals("machine", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"add_to_path 'scope' must be 'user' or 'machine', got '{pathScope}'.");
                }
                pal.Path.AddToPath(pathValue, pathScope);
                break;
            case "file_association":
                if (pal.FileAssociations is null)
                    throw new PlatformNotSupportedException("File association tasks are not supported on this platform.");

                var extension = GetString(p, "extension");
                var progId = GetOptionalString(p, "prog_id");

                if (string.IsNullOrEmpty(progId))
                {
                    var appName = InstallBootstrap.Manifest.Metadata.Name;
                    var safeAppName = new string(appName.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());
                    progId = $"{safeAppName}{extension}.1";
                }

                var assoc = new FileAssociationInfo
                {
                    Extension = extension,
                    Description = GetString(p, "description"),
                    ProgId = progId,
                    Icon = ExpandOptional(GetOptionalString(p, "icon"), pal),
                    Command = Expand(GetString(p, "command"), pal),
                    MimeType = GetOptionalString(p, "mime_type"),
                    BundlePath = ExpandOptional(GetOptionalString(p, "bundle_path"), pal),
                };

                if (isUninstall)
                {
                    pal.FileAssociations.Unregister(assoc);
                }
                else
                {
                    pal.FileAssociations.Register(assoc);
                }
                break;
            default:
                throw new NotSupportedException($"Unknown task action: '{task.Action}'.");
        }
    }

    private static string BuildShortcutPath(IPolyInstallPal pal, string location, string? subfolder, string name)
    {
        var isMachineScope = InstallBootstrap.Manifest?.Build?.Windows?.InstallScope
            ?.Equals("machine", StringComparison.OrdinalIgnoreCase) ?? false;

        string baseDir;
        if (OperatingSystem.IsWindows())
        {
            baseDir = location.ToLowerInvariant() switch
            {
                "start_menu" => isMachineScope
                    ? Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
                    : Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "desktop" => isMachineScope
                    ? Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
                    : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                _ => throw new NotSupportedException($"Unsupported shortcut location: '{location}'."),
            };
        }
        else if (OperatingSystem.IsLinux())
        {
            baseDir = location.ToLowerInvariant() switch
            {
                "start_menu" => Path.Combine(pal.UserHome, ".local", "share", "applications"),
                "desktop" => pal.Desktop,
                _ => throw new NotSupportedException($"Unsupported shortcut location: '{location}'."),
            };
        }
        else if (OperatingSystem.IsMacOS())
        {
            baseDir = location.ToLowerInvariant() switch
            {
                "start_menu" => Path.Combine(pal.UserHome, "Applications"),
                "desktop" => pal.Desktop,
                _ => throw new NotSupportedException($"Unsupported shortcut location: '{location}'."),
            };
        }
        else
        {
            throw new PlatformNotSupportedException("Shortcuts are not supported on this OS.");
        }

        // Manifest content is untrusted at runtime: shortcut names and subfolders must stay
        // inside the well-known shortcut directory.
        if (!string.IsNullOrEmpty(subfolder))
            RelativePathGuard.EnsureSimpleRelativePath(subfolder, "create_shortcut 'subfolder'");
        RelativePathGuard.EnsureSimpleFileName(name, "create_shortcut 'name'");

        if (OperatingSystem.IsWindows() && !name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            name += ".lnk";

        return RelativePathGuard.CombineConfined(baseDir,
            !string.IsNullOrEmpty(subfolder) ? [subfolder, name] : [name]);
    }

    /// <summary>
    /// Install tasks always execute on the machine running the installer (the install host),
    /// not the OS implied by <see cref="Hosting.InstallBootstrap.Manifest"/>'s build RID.
    /// </summary>
    private static string Expand(string s, IPolyInstallPal pal)
    {
        var hostOs = OperatingSystem.IsWindows()
            ? TargetOperatingSystem.Windows
            : OperatingSystem.IsMacOS()
                ? TargetOperatingSystem.MacOs
                : TargetOperatingSystem.Linux;
        return InstallPathResolver.Expand(s, pal, hostOs);
    }

    private static string? ExpandOptional(string? s, IPolyInstallPal pal)
        => s is null ? null : Expand(s, pal);

    private static string GetString(Dictionary<string, object?> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v is null)
            throw new InvalidOperationException($"Task parameter '{key}' is required.");
        return v switch
        {
            string s => s,
            JsonElement je => je.GetString() ?? "",
            _ => v.ToString() ?? "",
        };
    }

    private static string? GetOptionalString(Dictionary<string, object?> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v is null)
            return null;
        return v switch
        {
            string s => s,
            JsonElement je => je.GetString(),
            _ => v.ToString(),
        };
    }

    private static string GetStringOrDefault(Dictionary<string, object?> p, string key, string defaultValue)
    {
        if (!p.TryGetValue(key, out var v) || v is null)
            return defaultValue;
        return v switch
        {
            string s => s,
            JsonElement je => je.GetString() ?? defaultValue,
            _ => v.ToString() ?? defaultValue,
        };
    }

    /// <summary>
    /// Manifest modes are written the way chmod accepts them (<c>755</c>, <c>0644</c>), whether the
    /// YAML loader kept them as a number or a string and whether JSON later rehydrates them as a
    /// <see cref="JsonElement"/>. The integer handed to the PAL is the permission bitfield.
    /// </summary>
    private static int GetOctalMode(Dictionary<string, object?> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v is null)
            throw new InvalidOperationException($"Task parameter '{key}' is required.");

        var text = ModeText(v);
        if (!TryParseOctalMode(text, out var mode))
        {
            throw new InvalidOperationException(
                $"Task parameter '{key}' must be an octal permission such as 755 or 0644, got '{text}'.");
        }

        return mode;
    }

    private static string ModeText(object value) => value switch
    {
        string s => s.Trim(),
        JsonElement { ValueKind: JsonValueKind.String } je => je.GetString()?.Trim() ?? "",
        JsonElement je when je.ValueKind is JsonValueKind.Number => je.GetRawText().Trim(),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture)?.Trim() ?? "",
        _ => value.ToString()?.Trim() ?? "",
    };

    private static bool TryParseOctalMode(string text, out int mode)
    {
        mode = 0;
        if (text.Length is < 1 or > 4)
            return false;

        foreach (var c in text)
        {
            if (c is < '0' or > '7')
                return false;
        }

        mode = Convert.ToInt32(text, 8);
        return true;
    }
}
