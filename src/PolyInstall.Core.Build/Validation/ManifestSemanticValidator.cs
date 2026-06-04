using PolyInstall.Manifest;

namespace PolyInstall.Core.Build.Validation;

/// <summary>
/// Semantic validation of an <see cref="InstallManifest"/> that goes beyond JSON Schema checks.
/// Catches cross-field issues such as machine-wide registry writes for user-scope installs.
/// </summary>
public static class ManifestSemanticValidator
{
    public static void Validate(InstallManifest manifest)
    {
        var errors = new List<string>();
        ValidateTasks(manifest, errors);

        if (errors.Count == 0)
            return;

        var msg = string.Join(Environment.NewLine, errors);
        throw new InvalidOperationException(
            $"Manifest semantic validation failed:{Environment.NewLine}{msg}");
    }

    private static void ValidateTasks(InstallManifest manifest, List<string> errors)
    {
        var scope = (manifest.Build?.Windows?.InstallScope ?? "user").ToLowerInvariant();
        var isUserScope = scope == "user";

        var allTasks = new List<(InstallTask Task, string Phase)>();
        if (manifest.Tasks?.PreInstall is not null)
            allTasks.AddRange(manifest.Tasks.PreInstall.Select(t => (t, "pre_install")));
        if (manifest.Tasks?.PostInstall is not null)
            allTasks.AddRange(manifest.Tasks.PostInstall.Select(t => (t, "post_install")));
        if (manifest.Tasks?.PreUninstall is not null)
            allTasks.AddRange(manifest.Tasks.PreUninstall.Select(t => (t, "pre_uninstall")));
        if (manifest.Tasks?.PostUninstall is not null)
            allTasks.AddRange(manifest.Tasks.PostUninstall.Select(t => (t, "post_uninstall")));

        for (int i = 0; i < allTasks.Count; i++)
        {
            var (task, phase) = allTasks[i];
            var prefix = $"tasks.{phase}[{i}]";

            if (task.Action.Equals("create_shortcut", StringComparison.OrdinalIgnoreCase))
                ValidateCreateShortcut(task, prefix, errors);
            else if (task.Action.Equals("write_registry", StringComparison.OrdinalIgnoreCase))
                ValidateWriteRegistry(task, prefix, isUserScope, errors);
            else if (task.Action.Equals("create_desktop_entry", StringComparison.OrdinalIgnoreCase))
                ValidateCreateDesktopEntry(task, prefix, errors);
            else if (task.Action.Equals("set_permissions", StringComparison.OrdinalIgnoreCase))
                ValidateSetPermissions(task, prefix, errors);
        }
    }

    private static void ValidateCreateShortcut(InstallTask task, string prefix, List<string> errors)
    {
        if (!string.IsNullOrEmpty(GetParamString(task, "shortcut_path")))
        {
            errors.Add(
                $"{prefix}: create_shortcut no longer accepts 'shortcut_path'. " +
                "Use 'name', 'location' (start_menu or desktop), and optional 'subfolder' instead.");
        }

        var targetPath = GetParamString(task, "target_path");
        if (string.IsNullOrEmpty(targetPath))
            errors.Add($"{prefix}: create_shortcut requires parameter 'target_path'.");

        var name = GetParamString(task, "name");
        if (string.IsNullOrEmpty(name))
            errors.Add($"{prefix}: create_shortcut requires parameter 'name'.");

        var location = GetParamString(task, "location");
        if (string.IsNullOrEmpty(location))
        {
            errors.Add($"{prefix}: create_shortcut requires parameter 'location'.");
        }
        else if (!location.Equals("start_menu", StringComparison.OrdinalIgnoreCase)
                 && !location.Equals("desktop", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"{prefix}: create_shortcut 'location' must be 'start_menu' or 'desktop', got '{location}'.");
        }

        var subfolder = GetParamString(task, "subfolder");
        if (!string.IsNullOrEmpty(subfolder))
        {
            if (Path.IsPathRooted(subfolder) || subfolder.Contains("..", StringComparison.Ordinal))
            {
                errors.Add(
                    $"{prefix}: create_shortcut 'subfolder' must be a simple relative name, got '{subfolder}'.");
            }
        }
    }

    private static void ValidateWriteRegistry(InstallTask task, string prefix, bool isUserScope, List<string> errors)
    {
        var keyPath = GetParamString(task, "key_path");
        if (string.IsNullOrEmpty(keyPath))
        {
            errors.Add($"{prefix}: write_registry requires parameter 'key_path'.");
            return;
        }

        var parts = keyPath.Split('\\', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            errors.Add($"{prefix}: key_path must be in the form 'ROOT\\SubKey', e.g. 'HKCU\\Software\\MyApp'.");
            return;
        }

        var root = parts[0].ToUpperInvariant();
        var supportedRoots = new[] { "HKCU", "HKEY_CURRENT_USER", "HKLM", "HKEY_LOCAL_MACHINE" };
        if (!supportedRoots.Contains(root))
        {
            errors.Add(
                $"{prefix}: unsupported registry root '{parts[0]}'. Supported: HKCU, HKEY_CURRENT_USER, HKLM, HKEY_LOCAL_MACHINE.");
            return;
        }

        if (isUserScope && IsWindowsTask(task) && (root == "HKLM" || root == "HKEY_LOCAL_MACHINE"))
        {
            errors.Add(
                $"{prefix}: write_registry uses HKLM, but install_scope is 'user'. " +
                "Use HKCU or change install_scope to 'machine'.");
        }

        var valueKind = GetParamString(task, "value_kind");
        if (!string.IsNullOrEmpty(valueKind))
        {
            var validKinds = new[] { "string", "reg_sz", "expand_string", "reg_expand_sz", "dword", "reg_dword" };
            if (!validKinds.Contains(valueKind, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(
                    $"{prefix}: unsupported value_kind '{valueKind}'. Supported: string, reg_sz, expand_string, reg_expand_sz, dword, reg_dword.");
            }
        }
    }

    private static void ValidateCreateDesktopEntry(InstallTask task, string prefix, List<string> errors)
    {
        if (string.IsNullOrEmpty(GetParamString(task, "file_name")))
            errors.Add($"{prefix}: create_desktop_entry requires parameter 'file_name'.");
        if (string.IsNullOrEmpty(GetParamString(task, "name")))
            errors.Add($"{prefix}: create_desktop_entry requires parameter 'name'.");
        if (string.IsNullOrEmpty(GetParamString(task, "exec")))
            errors.Add($"{prefix}: create_desktop_entry requires parameter 'exec'.");
    }

    private static void ValidateSetPermissions(InstallTask task, string prefix, List<string> errors)
    {
        if (string.IsNullOrEmpty(GetParamString(task, "path")))
            errors.Add($"{prefix}: set_permissions requires parameter 'path'.");
        if (GetParamString(task, "mode") is null)
            errors.Add($"{prefix}: set_permissions requires parameter 'mode'.");
    }

    private static bool IsWindowsTask(InstallTask task)
    {
        var req = task.Require;
        if (string.IsNullOrWhiteSpace(req))
            return true;

        var r = req.Trim().ToLowerInvariant();
        return r.Contains("windows") || r.Contains("win");
    }

    private static string? GetParamString(InstallTask task, string key)
    {
        if (task.Parameters is null)
            return null;
        if (!task.Parameters.TryGetValue(key, out var v) || v is null)
            return null;
        return v.ToString();
    }
}
