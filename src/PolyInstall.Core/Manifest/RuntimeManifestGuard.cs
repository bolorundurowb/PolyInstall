using System.Text.Json;
using PolyInstall.Install;

namespace PolyInstall.Manifest;

/// <summary>
/// Security-relevant checks for manifests loaded from an installer bundle or from on-disk
/// install state. The CLI validator is not a runtime boundary.
/// </summary>
public static class RuntimeManifestGuard
{
    public static void Validate(InstallManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ValidateTaskPhase(manifest.Tasks?.PreInstall);
        ValidateTaskPhase(manifest.Tasks?.PostInstall);
        ValidateTaskPhase(manifest.Tasks?.PreUninstall);
        ValidateTaskPhase(manifest.Tasks?.PostUninstall);

        if (manifest.Services is not { Count: > 0 } services)
            return;

        foreach (var service in services)
        {
            if (string.IsNullOrWhiteSpace(service.Name) || !IsValidServiceName(service.Name))
                throw new InvalidOperationException(
                    $"Service name '{service.Name}' contains unsupported characters. Use letters, digits, '.', '_', or '-'.");
        }
    }

    /// <summary>
    /// Service names must be safe for <c>sc.exe</c> arguments, systemd unit file names, and
    /// launchd plist file names.
    /// </summary>
    public static bool IsValidServiceName(string name) =>
        name.Length > 0
        && name.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-')
        && !name.Contains("..", StringComparison.Ordinal)
        && !name.Contains(Path.DirectorySeparatorChar)
        && !name.Contains(Path.AltDirectorySeparatorChar);

    private static void ValidateTaskPhase(List<InstallTask>? tasks)
    {
        if (tasks is null)
            return;

        foreach (var task in tasks)
        {
            switch (task.Action.Trim().ToLowerInvariant())
            {
                case "create_shortcut":
                    RelativePathGuard.EnsureSimpleFileName(
                        GetParam(task, "name"), "create_shortcut 'name'");
                    var subfolder = GetParam(task, "subfolder");
                    if (!string.IsNullOrEmpty(subfolder))
                        RelativePathGuard.EnsureSimpleRelativePath(subfolder, "create_shortcut 'subfolder'");
                    break;
                case "create_desktop_entry":
                    RelativePathGuard.EnsureSimpleFileName(
                        GetParam(task, "file_name"), "create_desktop_entry 'file_name'");
                    break;
                case "add_to_path":
                    var scope = GetParam(task, "scope");
                    if (!string.IsNullOrEmpty(scope)
                        && !scope.Equals("user", StringComparison.OrdinalIgnoreCase)
                        && !scope.Equals("machine", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"add_to_path 'scope' must be 'user' or 'machine', got '{scope}'.");
                    }
                    break;
            }
        }
    }

    private static string? GetParam(InstallTask task, string key)
    {
        if (task.Parameters is null)
            return null;
        if (!task.Parameters.TryGetValue(key, out var value) || value is null)
            return null;
        return value switch
        {
            string text => text,
            JsonElement element => element.GetString(),
            _ => value.ToString(),
        };
    }
}
