using System.Text.Json;
using PolyInstall.Core.Conditions;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;

namespace PolyInstall.Core.Install;

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
                    GetString(p, "target_path"),
                    GetString(p, "shortcut_path"),
                    GetOptionalString(p, "description"),
                    GetOptionalString(p, "icon_path"));
                break;
            case "write_registry":
                if (pal.Registry is null)
                    throw new PlatformNotSupportedException("Registry tasks are not supported on this platform.");
                pal.Registry.SetValue(
                    GetString(p, "key_path"),
                    GetOptionalString(p, "value_name"),
                    GetString(p, "value"),
                    GetString(p, "value_kind"));
                break;
            case "create_desktop_entry":
                if (pal.DesktopEntries is null)
                    throw new PlatformNotSupportedException("Desktop entry tasks are not supported on this platform.");
                pal.DesktopEntries.CreateDesktopEntry(
                    GetString(p, "file_name"),
                    GetString(p, "name"),
                    GetString(p, "exec"),
                    GetOptionalString(p, "icon"),
                    GetOptionalString(p, "comment"));
                break;
            case "set_permissions":
                if (pal.FilePermissions is null)
                    throw new PlatformNotSupportedException("Permission tasks are not supported on this platform.");
                pal.FilePermissions.SetUnixFileMode(GetString(p, "path"), GetInt(p, "mode"));
                break;
            default:
                throw new NotSupportedException($"Unknown task action: '{task.Action}'.");
        }
    }

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
