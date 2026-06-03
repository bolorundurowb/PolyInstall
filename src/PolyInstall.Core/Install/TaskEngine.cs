using System.Text.Json;
using PolyInstall.Conditions;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Install;

public static class TaskEngine
{
    public static void RunPhase(IEnumerable<InstallTask>? tasks, IPolyInstallPal pal)
    {
        if (tasks is null)
            return;
        foreach (var task in tasks)
        {
            if (!ConditionEvaluator.Evaluate(task.Require))
                continue;
            Dispatch(task, pal);
        }
    }

    private static void Dispatch(InstallTask task, IPolyInstallPal pal)
    {
        var action = task.Action.Trim().ToLowerInvariant();
        var p = task.Parameters ?? new Dictionary<string, object?>();

        switch (action)
        {
            case "create_shortcut":
                pal.Shortcuts.CreateFileShortcut(
                    Expand(GetString(p, "target_path"), pal),
                    Expand(GetString(p, "shortcut_path"), pal),
                    ExpandOptional(GetOptionalString(p, "description"), pal),
                    ExpandOptional(GetOptionalString(p, "icon_path"), pal));
                break;
            case "write_registry":
                if (pal.Registry is null)
                    throw new PlatformNotSupportedException("Registry tasks are not supported on this platform.");
                pal.Registry.SetValue(
                    Expand(GetString(p, "key_path"), pal),
                    ExpandOptional(GetOptionalString(p, "value_name"), pal),
                    Expand(GetString(p, "value"), pal),
                    GetString(p, "value_kind"));
                break;
            case "create_desktop_entry":
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
                if (pal.FilePermissions is null)
                    throw new PlatformNotSupportedException("Permission tasks are not supported on this platform.");
                pal.FilePermissions.SetUnixFileMode(Expand(GetString(p, "path"), pal), GetInt(p, "mode"));
                break;
            default:
                throw new NotSupportedException($"Unknown task action: '{task.Action}'.");
        }
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

    private static int GetInt(Dictionary<string, object?> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v is null)
            throw new InvalidOperationException($"Task parameter '{key}' is required.");
        return v switch
        {
            int i => i,
            long l => (int)l,
            JsonElement je when je.TryGetInt32(out var x) => x,
            string s when int.TryParse(s, out var x) => x,
            _ => Convert.ToInt32(v),
        };
    }
}
