using PolyInstall.Build;
using PolyInstall.Install;
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

    /// <summary>
    /// Validates the manifest and throws an <see cref="InvalidOperationException"/> if semantic errors are found.
    /// </summary>
    /// <param name="manifest">The manifest to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if validation fails.</exception>
    public static void Validate(InstallManifest manifest)
    {
        var result = ValidateResult(manifest);
        if (result.IsValid)
            return;

        var msg = string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message));
        throw new InvalidOperationException(
            $"Manifest semantic validation failed:{Environment.NewLine}{msg}");
    }

    /// <summary>Collects semantic validation failures without throwing for invalid manifest content.</summary>
    public static ManifestValidationResult ValidateResult(InstallManifest manifest)
    {
        var errors = new List<ManifestDiagnostic>();
        ValidateMetadata(manifest, errors);
        ValidateBuild(manifest, errors);
        ValidateFeatures(manifest, errors);
        ValidateFiles(manifest, errors);
        ValidateWizardSteps(manifest, errors);
        ValidateTasks(manifest, errors);
        ValidateServices(manifest, errors);
        ValidateFileAssociations(manifest, errors);
        return errors.Count == 0
            ? ManifestValidationResult.Success
            : new ManifestValidationResult(errors);
    }

    private static void Add(
        List<ManifestDiagnostic> errors,
        string code,
        string message,
        string? path = null,
        string? help = null)
        => errors.Add(new ManifestDiagnostic(code, message, path, help));

    private static void ValidateFeatures(InstallManifest manifest, List<ManifestDiagnostic> errors)
    {
        var definedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (manifest.Features is { Count: > 0 } features)
        {
            for (int i = 0; i < features.Count; i++)
            {
                var feat = features[i];
                var path = $"features[{i}].id";
                if (string.IsNullOrWhiteSpace(feat.Id))
                {
                    Add(errors, "PI110", $"{path} must be non-empty.", path,
                        "Set a non-empty feature id.");
                    continue;
                }

                if (!definedIds.Add(feat.Id))
                {
                    Add(errors, "PI111",
                        $"{path} '{feat.Id}' is duplicated. Feature ids must be unique.", path,
                        "Give each feature a unique id.");
                }
            }
        }

        var anyReference = false;

        if (manifest.Files is { Count: > 0 })
        {
            for (int i = 0; i < manifest.Files.Count; i++)
            {
                var entry = manifest.Files[i];
                if (entry.Features is not { Count: > 0 } refs)
                    continue;
                anyReference = true;
                foreach (var fid in refs)
                {
                    if (!definedIds.Contains(fid))
                    {
                        var path = $"files[{i}].features";
                        Add(errors, "PI112",
                            $"{path} references unknown feature id '{fid}'.", path,
                            "Reference a feature id defined under 'features', or remove the reference.");
                    }
                }
            }
        }

        if (manifest.Tasks is not null)
        {
            CheckTaskFeatureRefs(manifest.Tasks.PreInstall, "pre_install", definedIds, errors, ref anyReference);
            CheckTaskFeatureRefs(manifest.Tasks.PostInstall, "post_install", definedIds, errors, ref anyReference);
            CheckTaskFeatureRefs(manifest.Tasks.PreUninstall, "pre_uninstall", definedIds, errors, ref anyReference);
            CheckTaskFeatureRefs(manifest.Tasks.PostUninstall, "post_uninstall", definedIds, errors, ref anyReference);
        }

        if (manifest.FileAssociations is { Count: > 0 } assocs)
        {
            for (int i = 0; i < assocs.Count; i++)
            {
                var assoc = assocs[i];
                if (assoc.Features is not { Count: > 0 } refs)
                    continue;
                anyReference = true;
                foreach (var fid in refs)
                {
                    if (!definedIds.Contains(fid))
                    {
                        var path = $"file_associations[{i}].features";
                        Add(errors, "PI112",
                            $"{path} references unknown feature id '{fid}'.", path,
                            "Reference a feature id defined under 'features', or remove the reference.");
                    }
                }
            }
        }

        if (manifest.Services is { Count: > 0 } services)
        {
            for (int i = 0; i < services.Count; i++)
            {
                var service = services[i];
                if (service.Features is not { Count: > 0 } refs)
                    continue;
                anyReference = true;
                foreach (var fid in refs)
                {
                    if (!definedIds.Contains(fid))
                    {
                        var path = $"services[{i}].features";
                        Add(errors, "PI112",
                            $"{path} references unknown feature id '{fid}'.", path,
                            "Reference a feature id defined under 'features', or remove the reference.");
                    }
                }
            }
        }

        if (anyReference && definedIds.Count == 0)
        {
            Add(errors, "PI113",
                "files, tasks, or file_associations reference features, but no features are defined at the manifest level. Define a 'features' list before referencing feature ids.",
                "features",
                "Add a top-level 'features' list, or remove feature references.");
        }
    }

    private static void ValidateMetadata(InstallManifest manifest, List<ManifestDiagnostic> errors)
    {
        if (string.IsNullOrWhiteSpace(manifest.Metadata.Name))
            Add(errors, "PI101", "metadata.name must be non-empty.", "metadata.name",
                "Set metadata.name to your product name.");
        if (string.IsNullOrWhiteSpace(manifest.Metadata.Version))
            Add(errors, "PI102", "metadata.version must be non-empty.", "metadata.version",
                "Set metadata.version to a non-empty version string.");
    }

    private static void CheckTaskFeatureRefs(
        List<InstallTask>? tasks,
        string phase,
        HashSet<string> definedIds,
        List<ManifestDiagnostic> errors,
        ref bool anyReference)
    {
        if (tasks is null) return;
        for (int i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            if (task.Features is not { Count: > 0 } refs)
                continue;
            anyReference = true;
            foreach (var fid in refs)
            {
                if (!definedIds.Contains(fid))
                {
                    var path = $"tasks.{phase}[{i}].features";
                    Add(errors, "PI112",
                        $"{path} references unknown feature id '{fid}'.", path,
                        "Reference a feature id defined under 'features', or remove the reference.");
                }
            }
        }
    }

    private static void ValidateFileAssociations(InstallManifest manifest, List<ManifestDiagnostic> errors)
    {
        if (manifest.FileAssociations is not { Count: > 0 } assocs)
            return;

        var hasMacOsTarget = HasTargetPrefix(manifest, "osx-");
        for (int i = 0; i < assocs.Count; i++)
        {
            var assoc = assocs[i];
            var prefix = $"file_associations[{i}]";

            if (string.IsNullOrWhiteSpace(assoc.Extension))
                Add(errors, "PI190", $"{prefix}.extension is required.", $"{prefix}.extension",
                    "Set extension to a dotted value such as '.oef'.");
            else if (!assoc.Extension.StartsWith('.'))
                Add(errors, "PI191", $"{prefix}.extension must start with a dot, e.g. '.oef'.",
                    $"{prefix}.extension", "Prefix the extension with a dot.");

            if (string.IsNullOrWhiteSpace(assoc.Description))
                Add(errors, "PI192", $"{prefix}.description is required.", $"{prefix}.description");

            if (string.IsNullOrWhiteSpace(assoc.Command))
                Add(errors, "PI193", $"{prefix}.command is required.", $"{prefix}.command");

            if (hasMacOsTarget && string.IsNullOrWhiteSpace(assoc.BundlePath))
                Add(errors, "PI194", $"{prefix}.bundle_path is required for macOS targets.",
                    $"{prefix}.bundle_path", "Set bundle_path to the .app bundle path for macOS.");
        }
    }

    private static void ValidateBuild(InstallManifest manifest, List<ManifestDiagnostic> errors)
    {
        if (manifest.Build.Targets.Count == 0)
            Add(errors, "PI103", "build.targets must contain at least one target.", "build.targets",
                "Add at least one supported target RID token.");

        for (var i = 0; i < manifest.Build.Targets.Count; i++)
        {
            var target = manifest.Build.Targets[i];
            if (!KnownTargetTokens.Contains(target))
            {
                Add(errors, "PI104",
                    $"build.targets contains unknown token '{target}'. Supported: {string.Join(", ", KnownTargetTokens)}.",
                    $"build.targets[{i}]",
                    "Use one of the supported target RID tokens.");
            }
        }

        var compression = manifest.Build.Compression.ToLowerInvariant();
        if (compression != "brotli" && compression != "gzip")
        {
            Add(errors, "PI105",
                $"build.compression must be 'brotli' or 'gzip', got '{manifest.Build.Compression}'.",
                "build.compression",
                "Set build.compression to 'brotli' or 'gzip'.");
        }

        ValidateSigning(manifest, errors);
    }

    private static void ValidateSigning(InstallManifest manifest, List<ManifestDiagnostic> errors)
    {
        var signing = manifest.Build.Signing;
        if (signing is null)
            return;

        if (signing.Linux is not null)
        {
            Add(errors, "PI120",
                "build.signing.linux is not supported. Linux outputs are unsigned by default; use an external detached-signature workflow if needed.",
                "build.signing.linux",
                "Remove build.signing.linux.");
        }

        if (signing.Windows is not null)
            ValidateWindowsSigning(manifest, signing.Windows, errors);

        if (signing.Macos is not null)
            ValidateMacOsSigning(manifest, signing.Macos, errors);
    }

    private static void ValidateWindowsSigning(
        InstallManifest manifest,
        WindowsSigningOptions options,
        List<ManifestDiagnostic> errors)
    {
        if (!HasTargetPrefix(manifest, "windows-"))
            Add(errors, "PI121",
                "build.signing.windows is configured, but build.targets does not contain a Windows target.",
                "build.signing.windows",
                "Add a windows-* target or remove Windows signing.");

        if (!string.IsNullOrWhiteSpace(options.CertificatePassword))
        {
            Add(errors, "PI122",
                "build.signing.windows.certificate_password stores a plaintext secret. Use certificate_password_env instead.",
                "build.signing.windows.certificate_password",
                "Move the secret into an environment variable and set certificate_password_env.");
        }

        var identitySources = CountNonEmpty(
            options.CertificatePath,
            options.CertificateThumbprint,
            options.CertificateSubject);
        if (identitySources == 0)
        {
            Add(errors, "PI123",
                "build.signing.windows requires one signing identity source: certificate_path, certificate_thumbprint, or certificate_subject.",
                "build.signing.windows",
                "Provide exactly one identity source.");
        }
        else if (identitySources > 1)
        {
            Add(errors, "PI124",
                "build.signing.windows must specify only one signing identity source: certificate_path, certificate_thumbprint, or certificate_subject.",
                "build.signing.windows",
                "Keep only one of certificate_path, certificate_thumbprint, or certificate_subject.");
        }

        if (!string.IsNullOrWhiteSpace(options.StoreLocation)
            && !IsOneOf(options.StoreLocation, "current_user", "local_machine"))
        {
            Add(errors, "PI125",
                $"build.signing.windows.store_location must be 'current_user' or 'local_machine', got '{options.StoreLocation}'.",
                "build.signing.windows.store_location");
        }

        if (!IsDigestAlgorithm(options.FileDigestAlgorithm))
            Add(errors, "PI126",
                "build.signing.windows.file_digest_algorithm must be 'sha1', 'sha256', 'sha384', or 'sha512'.",
                "build.signing.windows.file_digest_algorithm");

        if (!IsDigestAlgorithm(options.TimestampDigestAlgorithm))
            Add(errors, "PI127",
                "build.signing.windows.timestamp_digest_algorithm must be 'sha1', 'sha256', 'sha384', or 'sha512'.",
                "build.signing.windows.timestamp_digest_algorithm");
    }

    private static void ValidateMacOsSigning(
        InstallManifest manifest,
        MacOsSigningOptions options,
        List<ManifestDiagnostic> errors)
    {
        if (!HasTargetPrefix(manifest, "osx-"))
            Add(errors, "PI128",
                "build.signing.macos is configured, but build.targets does not contain a macOS target.",
                "build.signing.macos",
                "Add an osx-* target or remove macOS signing.");

        if (string.IsNullOrWhiteSpace(options.Identity))
            Add(errors, "PI129",
                "build.signing.macos.identity is required when macOS signing is configured.",
                "build.signing.macos.identity");

        if (!string.IsNullOrWhiteSpace(options.NotarizationProfile)
            && !string.Equals(manifest.Build.Macos?.Package, "dmg", StringComparison.OrdinalIgnoreCase))
        {
            Add(errors, "PI130",
                "build.signing.macos.notarization_profile requires build.macos.package: dmg.",
                "build.signing.macos.notarization_profile",
                "Set build.macos.package to dmg, or remove notarization_profile.");
        }
    }

    private static void ValidateFiles(InstallManifest manifest, List<ManifestDiagnostic> errors)
    {
        if (manifest.Files.Count == 0)
        {
            Add(errors, "PI106", "files must contain at least one entry.", "files",
                "Add at least one files entry with a relative source_dir.");
            return;
        }

        for (int i = 0; i < manifest.Files.Count; i++)
        {
            var entry = manifest.Files[i];
            var path = $"files[{i}].source_dir";
            if (Path.IsPathRooted(entry.SourceDir))
            {
                Add(errors, "PI107",
                    $"{path} must be a relative path, got absolute path '{entry.SourceDir}'.", path,
                    "Use a path relative to the manifest directory.");
                // Continue checking include so sibling issues still surface.
            }
            else if (entry.SourceDir.Contains("..", StringComparison.Ordinal))
            {
                Add(errors, "PI108",
                    $"{path} must not contain '..' directory traversal, got '{entry.SourceDir}'.", path,
                    "Remove '..' segments from source_dir.");
            }

            if (entry.Include.Count == 0)
            {
                Add(errors, "PI109",
                    $"files[{i}].include must contain at least one glob pattern.",
                    $"files[{i}].include",
                    "Add at least one include glob such as '**/*'.");
            }
        }
    }

    private static void ValidateWizardSteps(InstallManifest manifest, List<ManifestDiagnostic> errors)
    {
        var steps = manifest.Ui.WizardSteps;
        if (steps.Count == 0)
            return; // UI uses default steps

        var hasProgress = false;
        var hasDestination = false;
        var hasFeatures = false;
        var progressIndex = -1;
        var destinationIndex = -1;
        var featuresIndex = -1;

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
            else if (type == "features")
            {
                hasFeatures = true;
                featuresIndex = i;
            }
        }

        if (hasProgress && !hasDestination)
        {
            Add(errors, "PI140",
                "ui.wizard_steps contains a 'progress' step but no 'destination' step. A 'destination' step is required before 'progress' so the user can choose an install directory.",
                "ui.wizard_steps",
                "Add a destination step before progress.");
        }
        else if (hasProgress && hasDestination && destinationIndex > progressIndex)
        {
            Add(errors, "PI141",
                "ui.wizard_steps has 'destination' after 'progress'. The 'destination' step must come before the 'progress' step.",
                $"ui.wizard_steps[{destinationIndex}]",
                "Move the destination step above progress.");
        }

        if (hasFeatures)
        {
            if (hasDestination && featuresIndex < destinationIndex)
            {
                Add(errors, "PI142",
                    "ui.wizard_steps has 'features' before 'destination'. The 'features' step must come after the 'destination' step.",
                    $"ui.wizard_steps[{featuresIndex}]",
                    "Move the features step after destination.");
            }
            if (hasProgress && featuresIndex > progressIndex)
            {
                Add(errors, "PI143",
                    "ui.wizard_steps has 'features' after 'progress'. The 'features' step must come before the 'progress' step.",
                    $"ui.wizard_steps[{featuresIndex}]",
                    "Move the features step before progress.");
            }
        }
    }

    private static void ValidateTasks(InstallManifest manifest, List<ManifestDiagnostic> errors)
    {
        var scope = (manifest.Build.Windows?.InstallScope ?? "user").ToLowerInvariant();
        var isUserScope = scope == "user";

        ValidateTaskPhase(manifest.Tasks?.PreInstall, "pre_install", isUserScope, errors);
        ValidateTaskPhase(manifest.Tasks?.PostInstall, "post_install", isUserScope, errors);
        ValidateTaskPhase(manifest.Tasks?.PreUninstall, "pre_uninstall", isUserScope, errors);
        ValidateTaskPhase(manifest.Tasks?.PostUninstall, "post_uninstall", isUserScope, errors);
    }

    private static void ValidateTaskPhase(
        List<InstallTask>? tasks,
        string phase,
        bool isUserScope,
        List<ManifestDiagnostic> errors)
    {
        if (tasks is null)
            return;

        for (int i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
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
            else if (task.Action.Equals("file_association", StringComparison.OrdinalIgnoreCase))
                ValidateFileAssociation(task, prefix, errors);
            else
                Add(errors, "PI160", $"{prefix}: unknown task action '{task.Action}'.", prefix,
                    "Use a supported task action such as create_shortcut or write_registry.");
        }
    }

    private static void ValidateServices(InstallManifest manifest, List<ManifestDiagnostic> errors)
    {
        if (manifest.Services is not { Count: > 0 } services)
            return;

        for (int i = 0; i < services.Count; i++)
        {
            var service = services[i];
            var prefix = $"services[{i}]";

            if (string.IsNullOrWhiteSpace(service.Name))
                Add(errors, "PI180", $"{prefix}.name is required.", $"{prefix}.name");
            else if (!IsValidServiceName(service.Name))
                Add(errors, "PI181",
                    $"{prefix}.name '{service.Name}' contains unsupported characters. Use letters, digits, '.', '_', or '-'.",
                    $"{prefix}.name");

            if (string.IsNullOrWhiteSpace(service.Executable))
                Add(errors, "PI182", $"{prefix}.executable is required.", $"{prefix}.executable");

            var scope = string.IsNullOrWhiteSpace(service.Scope) ? "system" : service.Scope;
            if (!IsOneOf(scope, "system", "user", "machine"))
                Add(errors, "PI183", $"{prefix}.scope must be 'system' or 'user', got '{service.Scope}'.",
                    $"{prefix}.scope");

            var isWindows = HasOsPredicate(service.Require, "windows") || HasOsPredicate(service.Require, "win");
            var isLinux = HasOsPredicate(service.Require, "linux");
            var isMacOs = HasOsPredicate(service.Require, "macos") || HasOsPredicate(service.Require, "osx");
            var isUnix = HasOsPredicate(service.Require, "unix");

            if (!isWindows && !isLinux && !isMacOs && !isUnix)
            {
                Add(errors, "PI184",
                    $"{prefix}: services require an OS predicate (e.g., 'os.isWindows', 'os.isLinux', or 'os.isMacOS') to avoid runtime errors on unsupported platforms.",
                    prefix,
                    "Add a require predicate such as os.isWindows.");
            }

            if (isWindows && scope.Equals("user", StringComparison.OrdinalIgnoreCase))
                Add(errors, "PI185", $"{prefix}: Windows services support only scope 'system'.",
                    $"{prefix}.scope", "Set scope to 'system' for Windows services.");

            if ((isMacOs || isUnix) && !IsValidLaunchdRestart(service.Restart))
            {
                Add(errors, "PI186",
                    $"{prefix}.restart '{service.Restart}' cannot be mapped to launchd. Supported for macOS: always, on-failure.",
                    $"{prefix}.restart");
            }

            if (!IsValidSystemdRestart(service.Restart))
            {
                Add(errors, "PI187",
                    $"{prefix}.restart '{service.Restart}' is not a supported systemd restart policy. Supported: no, always, on-success, on-failure, on-abnormal, on-watchdog, on-abort.",
                    $"{prefix}.restart");
            }

            if (service.Environment is { Count: > 0 })
            {
                foreach (var key in service.Environment.Keys)
                {
                    if (string.IsNullOrWhiteSpace(key) || key.Any(c => !(char.IsLetterOrDigit(c) || c == '_')) || char.IsDigit(key[0]))
                        Add(errors, "PI188",
                            $"{prefix}.environment contains invalid variable name '{key}'. Use shell-style names such as MY_APP_HOME.",
                            $"{prefix}.environment");
                }
            }
        }
    }

    private static void ValidateCreateShortcut(InstallTask task, string prefix, List<ManifestDiagnostic> errors)
    {
        if (!string.IsNullOrEmpty(GetParamString(task, "shortcut_path")))
        {
            Add(errors, "PI161",
                $"{prefix}: create_shortcut no longer accepts 'shortcut_path'. " +
                "Use 'name', 'location' (start_menu or desktop), and optional 'subfolder' instead.",
                prefix,
                "Replace shortcut_path with name, location, and optional subfolder.");
        }

        var targetPath = GetParamString(task, "target_path");
        if (string.IsNullOrEmpty(targetPath))
            Add(errors, "PI162", $"{prefix}: create_shortcut requires parameter 'target_path'.", prefix);

        var name = GetParamString(task, "name");
        if (string.IsNullOrEmpty(name))
            Add(errors, "PI163", $"{prefix}: create_shortcut requires parameter 'name'.", prefix);
        else if (!RelativePathGuard.IsSimpleFileName(name))
            Add(errors, "PI203",
                $"{prefix}: create_shortcut 'name' must be a simple file name without path separators or '..', got '{name}'.",
                prefix);

        var location = GetParamString(task, "location");
        if (string.IsNullOrEmpty(location))
        {
            Add(errors, "PI164", $"{prefix}: create_shortcut requires parameter 'location'.", prefix);
        }
        else if (!location.Equals("start_menu", StringComparison.OrdinalIgnoreCase)
                 && !location.Equals("desktop", StringComparison.OrdinalIgnoreCase))
        {
            Add(errors, "PI165",
                $"{prefix}: create_shortcut 'location' must be 'start_menu' or 'desktop', got '{location}'.",
                prefix,
                "Set location to start_menu or desktop.");
        }

        var subfolder = GetParamString(task, "subfolder");
        if (!string.IsNullOrEmpty(subfolder))
        {
            if (Path.IsPathRooted(subfolder) || subfolder.Contains("..", StringComparison.Ordinal))
            {
                Add(errors, "PI166",
                    $"{prefix}: create_shortcut 'subfolder' must be a simple relative name, got '{subfolder}'.",
                    prefix);
            }
        }
    }

    private static void ValidateWriteRegistry(InstallTask task, string prefix, bool isUserScope, List<ManifestDiagnostic> errors)
    {
        if (!HasOsPredicate(task, "windows"))
        {
            Add(errors, "PI167",
                $"{prefix}: write_registry is Windows-only. Add require: 'os.isWindows' (or similar) to avoid runtime errors on other platforms.",
                prefix,
                "Add require: os.isWindows.");
        }

        var keyPath = GetParamString(task, "key_path");
        if (string.IsNullOrEmpty(keyPath))
        {
            Add(errors, "PI168", $"{prefix}: write_registry requires parameter 'key_path'.", prefix);
            return;
        }

        var parts = keyPath.Split('\\', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            Add(errors, "PI169",
                $"{prefix}: key_path must be in the form 'ROOT\\SubKey', e.g. 'HKCU\\Software\\MyApp'.",
                prefix);
            return;
        }

        var root = parts[0].ToUpperInvariant();
        var supportedRoots = new[] { "HKCU", "HKEY_CURRENT_USER", "HKLM", "HKEY_LOCAL_MACHINE" };
        if (!supportedRoots.Contains(root))
        {
            Add(errors, "PI170",
                $"{prefix}: unsupported registry root '{parts[0]}'. Supported: HKCU, HKEY_CURRENT_USER, HKLM, HKEY_LOCAL_MACHINE.",
                prefix,
                "Use HKCU or HKLM.");
            return;
        }

        if (isUserScope && IsWindowsTask(task) && (root == "HKLM" || root == "HKEY_LOCAL_MACHINE"))
        {
            Add(errors, "PI171",
                $"{prefix}: write_registry uses HKLM, but install_scope is 'user'. " +
                "Use HKCU or change install_scope to 'machine'.",
                prefix,
                "Use HKCU, or set build.windows.install_scope to 'machine'.");
        }

        var valueKind = GetParamString(task, "value_kind");
        if (!string.IsNullOrEmpty(valueKind))
        {
            var validKinds = new[] { "string", "reg_sz", "expand_string", "reg_expand_sz", "dword", "reg_dword" };
            if (!validKinds.Contains(valueKind, StringComparer.OrdinalIgnoreCase))
            {
                Add(errors, "PI172",
                    $"{prefix}: unsupported value_kind '{valueKind}'. Supported: string, reg_sz, expand_string, reg_expand_sz, dword, reg_dword.",
                    prefix);
            }
        }
    }

    private static void ValidateCreateDesktopEntry(InstallTask task, string prefix, List<ManifestDiagnostic> errors)
    {
        if (!HasOsPredicate(task, "linux") && !HasOsPredicate(task, "unix"))
        {
            Add(errors, "PI173",
                $"{prefix}: create_desktop_entry is Linux-only. Add require: 'os.isLinux' to avoid runtime errors on other platforms.",
                prefix,
                "Add require: os.isLinux.");
        }

        var fileName = GetParamString(task, "file_name");
        if (string.IsNullOrEmpty(fileName))
            Add(errors, "PI174", $"{prefix}: create_desktop_entry requires parameter 'file_name'.", prefix);
        else if (!RelativePathGuard.IsSimpleFileName(fileName))
            Add(errors, "PI204",
                $"{prefix}: create_desktop_entry 'file_name' must be a simple file name without path separators or '..', got '{fileName}'.",
                prefix);
        if (string.IsNullOrEmpty(GetParamString(task, "name")))
            Add(errors, "PI175", $"{prefix}: create_desktop_entry requires parameter 'name'.", prefix);
        if (string.IsNullOrEmpty(GetParamString(task, "exec")))
            Add(errors, "PI176", $"{prefix}: create_desktop_entry requires parameter 'exec'.", prefix);
    }

    private static void ValidateSetPermissions(InstallTask task, string prefix, List<ManifestDiagnostic> errors)
    {
        if (!HasOsPredicate(task, "linux") && !HasOsPredicate(task, "macos") && !HasOsPredicate(task, "unix"))
        {
            Add(errors, "PI177",
                $"{prefix}: set_permissions is Linux/macOS-only. Add require: 'os.isLinux', 'os.isMacOS', or 'os.isUnix' to avoid runtime errors on other platforms.",
                prefix);
        }

        if (string.IsNullOrEmpty(GetParamString(task, "path")))
            Add(errors, "PI178", $"{prefix}: set_permissions requires parameter 'path'.", prefix);
        if (GetParamString(task, "mode") is null)
            Add(errors, "PI179", $"{prefix}: set_permissions requires parameter 'mode'.", prefix);
    }

    private static void ValidateAddToPath(InstallTask task, string prefix, bool isUserScope, List<ManifestDiagnostic> errors)
    {
        var scope = GetParamString(task, "scope") ?? "user";
        if (!scope.Equals("user", StringComparison.OrdinalIgnoreCase)
            && !scope.Equals("machine", StringComparison.OrdinalIgnoreCase))
        {
            Add(errors, "PI195",
                $"{prefix}: add_to_path 'scope' must be 'user' or 'machine', got '{scope}'.", prefix);
        }

        if (scope.Equals("machine", StringComparison.OrdinalIgnoreCase) && isUserScope)
        {
            Add(errors, "PI196",
                $"{prefix}: add_to_path with scope 'machine' requires install_scope 'machine'. " +
                "Machine-level PATH modification requires Administrator rights.",
                prefix,
                "Set install_scope to machine, or use PATH scope 'user'.");
        }
    }

    private static void ValidateFileAssociation(InstallTask task, string prefix, List<ManifestDiagnostic> errors)
    {
        var isWindows = HasOsPredicate(task, "windows");
        var isLinux = HasOsPredicate(task, "linux") || HasOsPredicate(task, "unix");
        var isMacOs = HasOsPredicate(task, "macos") || HasOsPredicate(task, "osx");

        if (!isWindows && !isLinux && !isMacOs)
        {
            Add(errors, "PI197",
                $"{prefix}: file_association requires an OS predicate (e.g., 'os.isWindows', 'os.isLinux', or 'os.isMacOS') to avoid runtime errors on unsupported platforms.",
                prefix);
        }

        var extension = GetParamString(task, "extension");
        if (string.IsNullOrEmpty(extension))
        {
            Add(errors, "PI198", $"{prefix}: file_association requires parameter 'extension'.", prefix);
        }
        else if (!extension.StartsWith('.'))
        {
            Add(errors, "PI199",
                $"{prefix}: file_association 'extension' must start with a dot, e.g. '.oef'.", prefix);
        }

        if (string.IsNullOrEmpty(GetParamString(task, "description")))
            Add(errors, "PI200", $"{prefix}: file_association requires parameter 'description'.", prefix);

        if (string.IsNullOrEmpty(GetParamString(task, "command")))
            Add(errors, "PI201", $"{prefix}: file_association requires parameter 'command'.", prefix);

        if (isMacOs && string.IsNullOrEmpty(GetParamString(task, "bundle_path")))
            Add(errors, "PI202", $"{prefix}: file_association on macOS requires parameter 'bundle_path'.", prefix);
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

    private static bool HasOsPredicate(string? require, string osKeyword)
    {
        if (string.IsNullOrWhiteSpace(require))
            return false;

        var r = require.Trim().ToLowerInvariant();
        return r.Contains(osKeyword);
    }

    private static bool IsValidServiceName(string name) =>
        RuntimeManifestGuard.IsValidServiceName(name);

    private static bool IsValidSystemdRestart(string? restart) =>
        string.IsNullOrWhiteSpace(restart)
        || IsOneOf(restart, "no", "always", "on-success", "on-failure", "on-abnormal", "on-watchdog", "on-abort");

    private static bool IsValidLaunchdRestart(string? restart) =>
        string.IsNullOrWhiteSpace(restart)
        || IsOneOf(restart, "always", "on-failure");

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
