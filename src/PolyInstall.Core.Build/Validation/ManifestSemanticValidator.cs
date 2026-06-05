using PolyInstall.Build;
using PolyInstall.Manifest;

namespace PolyInstall.Core.Build.Validation;

/// <summary>
/// Semantic validation of an <see cref="InstallManifest"/> that goes beyond JSON Schema checks.
/// Catches cross-field issues such as machine-wide registry writes for user-scope installs,
/// missing destination steps, and OS/task mismatches.
/// </summary>
public static class ManifestSemanticValidator
{
    private static readonly HashSet<string> KnownTargetTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows-x64", "windows-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64",
    };

    public static void Validate(InstallManifest manifest)
    {
        var errors = new List<string>();
        ValidateBuild(manifest, errors);
        ValidateFiles(manifest, errors);
        ValidateWizardSteps(manifest, errors);
        ValidateTasks(manifest, errors);

        if (errors.Count == 0)
            return;

        var msg = string.Join(Environment.NewLine, errors);
        throw new InvalidOperationException(
            $"Manifest semantic validation failed:{Environment.NewLine}{msg}");
    }

    private static void ValidateBuild(InstallManifest manifest, List<string> errors)
    {
        if (manifest.Build.Targets.Count == 0)
            errors.Add("build.targets must contain at least one target.");

        foreach (var target in manifest.Build.Targets)
        {
            if (!KnownTargetTokens.Contains(target))
                errors.Add($"build.targets contains unknown token '{target}'. Supported: {string.Join(", ", KnownTargetTokens)}.");
        }

        var compression = manifest.Build.Compression.ToLowerInvariant();
        if (compression != "brotli" && compression != "gzip")
            errors.Add($"build.compression must be 'brotli' or 'gzip', got '{manifest.Build.Compression}'.");

        ValidateSigning(manifest, errors);
    }

    private static void ValidateSigning(InstallManifest manifest, List<string> errors)
    {
        var signing = manifest.Build.Signing;
        if (signing is null)
            return;

        if (signing.Linux is not null)
        {
            errors.Add(
                "build.signing.linux is not supported. Linux outputs are unsigned by default; use an external detached-signature workflow if needed.");
        }

        if (signing.Windows is not null)
            ValidateWindowsSigning(manifest, signing.Windows, errors);

        if (signing.Macos is not null)
            ValidateMacOsSigning(manifest, signing.Macos, errors);
    }

    private static void ValidateWindowsSigning(
        InstallManifest manifest,
        WindowsSigningOptions options,
        List<string> errors)
    {
        if (!HasTargetPrefix(manifest, "windows-"))
            errors.Add("build.signing.windows is configured, but build.targets does not contain a Windows target.");

        if (!string.IsNullOrWhiteSpace(options.CertificatePassword))
        {
            errors.Add(
                "build.signing.windows.certificate_password stores a plaintext secret. Use certificate_password_env instead.");
        }

        var identitySources = CountNonEmpty(
            options.CertificatePath,
            options.CertificateThumbprint,
            options.CertificateSubject);
        if (identitySources == 0)
        {
            errors.Add(
                "build.signing.windows requires one signing identity source: certificate_path, certificate_thumbprint, or certificate_subject.");
        }
        else if (identitySources > 1)
        {
            errors.Add(
                "build.signing.windows must specify only one signing identity source: certificate_path, certificate_thumbprint, or certificate_subject.");
        }

        if (!string.IsNullOrWhiteSpace(options.StoreLocation)
            && !IsOneOf(options.StoreLocation, "current_user", "local_machine"))
        {
            errors.Add(
                $"build.signing.windows.store_location must be 'current_user' or 'local_machine', got '{options.StoreLocation}'.");
        }

        if (!IsDigestAlgorithm(options.FileDigestAlgorithm))
            errors.Add("build.signing.windows.file_digest_algorithm must be 'sha1', 'sha256', 'sha384', or 'sha512'.");

        if (!IsDigestAlgorithm(options.TimestampDigestAlgorithm))
            errors.Add("build.signing.windows.timestamp_digest_algorithm must be 'sha1', 'sha256', 'sha384', or 'sha512'.");
    }

    private static void ValidateMacOsSigning(
        InstallManifest manifest,
        MacOsSigningOptions options,
        List<string> errors)
    {
        if (!HasTargetPrefix(manifest, "osx-"))
            errors.Add("build.signing.macos is configured, but build.targets does not contain a macOS target.");

        if (string.IsNullOrWhiteSpace(options.Identity))
            errors.Add("build.signing.macos.identity is required when macOS signing is configured.");

        if (!string.IsNullOrWhiteSpace(options.NotarizationProfile)
            && !string.Equals(manifest.Build.Macos?.Package, "dmg", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("build.signing.macos.notarization_profile requires build.macos.package: dmg.");
        }
    }

    private static void ValidateFiles(InstallManifest manifest, List<string> errors)
    {
        if (manifest.Files.Count == 0)
        {
            errors.Add("files must contain at least one entry.");
            return;
        }

        for (int i = 0; i < manifest.Files.Count; i++)
        {
            var entry = manifest.Files[i];
            if (Path.IsPathRooted(entry.SourceDir))
            {
                errors.Add($"files[{i}].source_dir must be a relative path, got absolute path '{entry.SourceDir}'.");
                continue;
            }

            if (entry.SourceDir.Contains("..", StringComparison.Ordinal))
            {
                errors.Add($"files[{i}].source_dir must not contain '..' directory traversal, got '{entry.SourceDir}'.");
            }

            if (entry.Include.Count == 0)
            {
                errors.Add($"files[{i}].include must contain at least one glob pattern.");
            }
        }
    }

    private static void ValidateWizardSteps(InstallManifest manifest, List<string> errors)
    {
        var steps = manifest.Ui.WizardSteps;
        if (steps.Count == 0)
            return; // UI uses default steps

        var hasProgress = false;
        var hasDestination = false;
        var progressIndex = -1;
        var destinationIndex = -1;

        for (int i = 0; i < steps.Count; i++)
        {
            var type = steps[i].Type.Trim().ToLowerInvariant();
            if (type == "progress")
            {
                hasProgress = true;
                progressIndex = i;
            }
            else if (type == "destination")
            {
                hasDestination = true;
                destinationIndex = i;
            }
        }

        if (hasProgress && !hasDestination)
            errors.Add("ui.wizard_steps contains a 'progress' step but no 'destination' step. A 'destination' step is required before 'progress' so the user can choose an install directory.");
        else if (hasProgress && hasDestination && destinationIndex > progressIndex)
            errors.Add("ui.wizard_steps has 'destination' after 'progress'. The 'destination' step must come before the 'progress' step.");
    }

    private static void ValidateTasks(InstallManifest manifest, List<string> errors)
    {
        var scope = (manifest.Build.Windows?.InstallScope ?? "user").ToLowerInvariant();
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
            else if (task.Action.Equals("add_to_path", StringComparison.OrdinalIgnoreCase))
                ValidateAddToPath(task, prefix, isUserScope, errors);
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
        if (!HasOsPredicate(task, "windows"))
        {
            errors.Add(
                $"{prefix}: write_registry is Windows-only. Add require: 'os.isWindows' (or similar) to avoid runtime errors on other platforms.");
        }

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
        if (!HasOsPredicate(task, "linux") && !HasOsPredicate(task, "macos") && !HasOsPredicate(task, "unix"))
        {
            errors.Add(
                $"{prefix}: create_desktop_entry is Linux/macOS-only. Add require: 'os.isLinux' or 'os.isUnix' to avoid runtime errors on other platforms.");
        }

        if (string.IsNullOrEmpty(GetParamString(task, "file_name")))
            errors.Add($"{prefix}: create_desktop_entry requires parameter 'file_name'.");
        if (string.IsNullOrEmpty(GetParamString(task, "name")))
            errors.Add($"{prefix}: create_desktop_entry requires parameter 'name'.");
        if (string.IsNullOrEmpty(GetParamString(task, "exec")))
            errors.Add($"{prefix}: create_desktop_entry requires parameter 'exec'.");
    }

    private static void ValidateSetPermissions(InstallTask task, string prefix, List<string> errors)
    {
        if (!HasOsPredicate(task, "linux") && !HasOsPredicate(task, "macos") && !HasOsPredicate(task, "unix"))
        {
            errors.Add(
                $"{prefix}: set_permissions is Unix-only. Add require: 'os.isLinux' or 'os.isUnix' to avoid runtime errors on other platforms.");
        }

        if (string.IsNullOrEmpty(GetParamString(task, "path")))
            errors.Add($"{prefix}: set_permissions requires parameter 'path'.");
        if (GetParamString(task, "mode") is null)
            errors.Add($"{prefix}: set_permissions requires parameter 'mode'.");
    }

    private static void ValidateAddToPath(InstallTask task, string prefix, bool isUserScope, List<string> errors)
    {
        var scope = GetParamString(task, "scope") ?? "user";
        if (!scope.Equals("user", StringComparison.OrdinalIgnoreCase)
            && !scope.Equals("machine", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{prefix}: add_to_path 'scope' must be 'user' or 'machine', got '{scope}'.");
        }

        if (scope.Equals("machine", StringComparison.OrdinalIgnoreCase) && isUserScope)
        {
            errors.Add(
                $"{prefix}: add_to_path with scope 'machine' requires install_scope 'machine'. " +
                "Machine-level PATH modification requires Administrator rights.");
        }
    }

    private static bool IsWindowsTask(InstallTask task)
    {
        var req = task.Require;
        if (string.IsNullOrWhiteSpace(req))
            return true;

        var r = req.Trim().ToLowerInvariant();
        return r.Contains("windows") || r.Contains("win");
    }

    private static bool HasOsPredicate(InstallTask task, string osKeyword)
    {
        var req = task.Require;
        if (string.IsNullOrWhiteSpace(req))
            return false;

        var r = req.Trim().ToLowerInvariant();
        return r.Contains(osKeyword);
    }

    private static bool HasTargetPrefix(InstallManifest manifest, string prefix)
    {
        return manifest.Build.Targets.Any(t => t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static int CountNonEmpty(params string?[] values)
    {
        return values.Count(v => !string.IsNullOrWhiteSpace(v));
    }

    private static bool IsOneOf(string value, params string[] allowed)
    {
        return allowed.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsDigestAlgorithm(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || IsOneOf(value, "sha1", "sha256", "sha384", "sha512");
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
