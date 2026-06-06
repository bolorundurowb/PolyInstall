using PolyInstall.Conditions;
using PolyInstall.Hosting;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Install;

public static class InstallCoordinator
{
    public static InstallOperationResult Run(InstallOperationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.Manifest);
        ArgumentNullException.ThrowIfNull(options.Pal);

        var dest = InstallPathResolver.Expand(options.Destination.Trim(), options.Pal);
        if (string.IsNullOrWhiteSpace(dest))
            throw new InvalidOperationException("No install directory.");

        var existing = ResolveExistingInstall(options.Manifest, dest, options.ExistingInstall);
        var mode = ResolveMode(options.Manifest, existing);
        var existedBefore = Directory.Exists(dest);

        InstallBootstrap.InstallDirectory = dest;
        options.CancellationToken.ThrowIfCancellationRequested();
        options.Progress?.Invoke($"Prepare folder: {dest}");
        Directory.CreateDirectory(dest);
        options.OnInstallDirectoryPrepared?.Invoke(new InstallDirectoryPreparedInfo(dest, existedBefore, !existedBefore, mode));
        options.Progress?.Invoke(existedBefore ? $"Use existing folder: {dest}" : $"Create folder: {dest}");

        var selectedFeatures = ResolveSelectedFeatures(options.Manifest);
        var activeServices = ResolveActiveServices(options.Manifest, options.Pal, selectedFeatures);

        options.CancellationToken.ThrowIfCancellationRequested();
        options.Progress?.Invoke("Run pre-install tasks");
        TaskEngine.RunPhase(options.Manifest.Tasks?.PreInstall, options.Pal, selectedFeatures);
        options.Progress?.Invoke("Pre-install tasks completed");

        options.CancellationToken.ThrowIfCancellationRequested();
        RemoveStaleServices(existing?.State?.RegisteredServices, activeServices, options.Pal, options.Progress);

        options.CancellationToken.ThrowIfCancellationRequested();
        var allPayloadFiles = PayloadFileInventory.Enumerate(options.ExtractRoot);
        var allowedFiles = FeatureFilter.ComputeAllowedFiles(
            options.Manifest.FeatureIndex,
            allPayloadFiles,
            selectedFeatures);
        var installedPayloadFiles = allPayloadFiles
            .Where(f => allowedFiles.Contains(PayloadFileInventory.NormalizeRelativePath(f)))
            .ToList();
        var newPayloadSet = installedPayloadFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (existing?.State?.PayloadFiles is { } previousPayloadFiles)
        {
            PayloadFileInventory.DeleteFilesMissingFromNewPayload(
                dest,
                previousPayloadFiles,
                newPayloadSet,
                relativePath => options.Progress?.Invoke($"Remove obsolete file: {relativePath}"));
        }
        else if (existing is not null)
        {
            options.Progress?.Invoke("No previous file inventory found; obsolete payload cleanup skipped");
        }

        options.CancellationToken.ThrowIfCancellationRequested();
        options.Progress?.Invoke(mode == InstallMode.Install ? "Copy files" : "Update files");
        DirectoryCopy.CopyRecursive(
            options.ExtractRoot,
            dest,
            allowedFiles,
            options.CancellationToken,
            relativePath => options.Progress?.Invoke($"Copy file: {relativePath}"));
        options.Progress?.Invoke(mode == InstallMode.Install ? "Files copied" : "Files updated");

        options.CancellationToken.ThrowIfCancellationRequested();
        options.Progress?.Invoke("Run post-install tasks");
        TaskEngine.RunPhase(options.Manifest.Tasks?.PostInstall, options.Pal, selectedFeatures);
        options.Progress?.Invoke("Post-install tasks completed");

        if (options.Manifest.FileAssociations is { Count: > 0 } && options.Pal.FileAssociations is not null)
        {
            options.Progress?.Invoke("Register file associations");
            foreach (var assoc in options.Manifest.FileAssociations)
            {
                if (!FeatureFilter.IsActive(assoc.Features, selectedFeatures))
                    continue;
                var info = MapToFileAssociationInfo(assoc, options.Pal);
                options.Pal.FileAssociations.Register(info);
            }
            options.Progress?.Invoke("File associations registered");
        }

        var registeredServices = InstallServices(activeServices, options.Pal, options.Progress);

        options.CancellationToken.ThrowIfCancellationRequested();
        options.Progress?.Invoke("Write install metadata");
        var state = InstallFinalizer.FinalizeInstall(
            options.Manifest,
            dest,
            installedPayloadFiles,
            selectedFeatures);

        var stateUpdated = false;
        if (registeredServices.Count > 0)
        {
            state.RegisteredServices = registeredServices
                .OrderBy(s => s.Platform, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Scope, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            stateUpdated = true;
        }

        if (options.Pal.Path is { AddedPaths.Count: > 0 } pathPal)
        {
            state.AddedToPath = pathPal.AddedPaths.Select(a => a.Path).ToList();
            stateUpdated = true;
            options.Progress?.Invoke("PATH entries recorded");
        }

        if (stateUpdated)
            InstallStateIo.WriteState(dest, state);

        options.Progress?.Invoke("Install metadata written");

        if (OperatingSystem.IsWindows() && (options.Manifest.Build.Windows?.RegisterArp ?? true))
            options.Progress?.Invoke("Add/Remove Programs entry registered");

        return new InstallOperationResult(dest, mode, existedBefore, !existedBefore, state);
    }

    private static ExistingInstallInfo? ResolveExistingInstall(
        InstallManifest manifest,
        string destination,
        ExistingInstallInfo? knownExisting)
    {
        if (knownExisting is not null
            && SamePath(knownExisting.InstallLocation, destination))
        {
            return knownExisting;
        }

        return InstalledProductLocator.TryReadFromInstallDirectory(manifest, destination);
    }

    public static InstallMode ResolveMode(InstallManifest manifest, ExistingInstallInfo? existing)
    {
        if (existing is null)
            return InstallMode.Install;

        return existing.DisplayVersion.Equals(manifest.Metadata.Version, StringComparison.OrdinalIgnoreCase)
            ? InstallMode.Repair
            : InstallMode.Update;
    }

    private static bool SamePath(string left, string right)
    {
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(left))
                .Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return left.Equals(right, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Resolves the active feature set for this install run from <see cref="InstallBootstrap.SelectedFeatures"/>,
    /// falling back to all default-selected manifest features. Returns an empty set when no features are defined.
    /// </summary>
    private static IReadOnlySet<string> ResolveSelectedFeatures(InstallManifest manifest)
    {
        var bootstrap = InstallBootstrap.SelectedFeatures;
        if (bootstrap is { Count: > 0 })
            return bootstrap;

        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (manifest.Features is { Count: > 0 } features)
        {
            foreach (var feat in features)
            {
                if (!string.IsNullOrWhiteSpace(feat.Id) && feat.DefaultSelected)
                    selected.Add(feat.Id);
            }
        }

        return selected;
    }

    internal static FileAssociationInfo MapToFileAssociationInfo(Manifest.FileAssociation assoc, IPolyInstallPal pal)
    {
        var progId = assoc.ProgId;
        if (string.IsNullOrEmpty(progId))
        {
            var appName = InstallBootstrap.Manifest.Metadata.Name;
            var safeAppName = new string(appName.Where(c => char.IsLetterOrDigit(c) || c == '.').ToArray());
            progId = $"{safeAppName}{assoc.Extension}.1";
        }

        return new FileAssociationInfo
        {
            Extension = assoc.Extension,
            Description = assoc.Description,
            ProgId = progId,
            Icon = string.IsNullOrEmpty(assoc.Icon) ? null : InstallPathResolver.Expand(assoc.Icon, pal),
            Command = InstallPathResolver.Expand(assoc.Command, pal),
            MimeType = assoc.MimeType,
            BundlePath = string.IsNullOrEmpty(assoc.BundlePath) ? null : InstallPathResolver.Expand(assoc.BundlePath, pal),
        };
    }

    private static List<ServiceRegistrationInfo> ResolveActiveServices(
        InstallManifest manifest,
        IPolyInstallPal pal,
        IReadOnlySet<string> selectedFeatures)
    {
        var services = new List<ServiceRegistrationInfo>();
        if (manifest.Services is not { Count: > 0 })
            return services;

        foreach (var service in manifest.Services)
        {
            if (!ConditionEvaluator.Evaluate(service.Require))
                continue;
            if (!FeatureFilter.IsActive(service.Features, selectedFeatures))
                continue;

            services.Add(MapToServiceRegistrationInfo(service, pal));
        }

        return services;
    }

    internal static ServiceRegistrationInfo MapToServiceRegistrationInfo(ServiceDefinition service, IPolyInstallPal pal)
    {
        return new ServiceRegistrationInfo
        {
            Name = service.Name,
            DisplayName = service.DisplayName,
            Description = service.Description,
            Scope = NormalizeServiceScope(service.Scope),
            Enabled = service.Enabled,
            Start = service.Start,
            Executable = InstallPathResolver.Expand(service.Executable, pal),
            Arguments = service.Arguments?.Select(a => InstallPathResolver.Expand(a, pal)).ToList() ?? [],
            WorkingDirectory = string.IsNullOrWhiteSpace(service.WorkingDirectory)
                ? null
                : InstallPathResolver.Expand(service.WorkingDirectory, pal),
            Restart = service.Restart,
            Environment = service.Environment?.ToDictionary(
                e => e.Key,
                e => InstallPathResolver.Expand(e.Value, pal),
                StringComparer.Ordinal),
        };
    }

    private static List<RegisteredServiceInfo> InstallServices(
        IReadOnlyCollection<ServiceRegistrationInfo> activeServices,
        IPolyInstallPal pal,
        Action<string>? progress)
    {
        if (activeServices.Count == 0)
            return [];
        if (pal.Services is null)
            throw new PlatformNotSupportedException("Service management is not supported on this platform.");

        progress?.Invoke("Register services");
        foreach (var service in activeServices)
        {
            pal.Services.InstallOrUpdate(service);
            progress?.Invoke($"Registered service: {service.Name}");
        }

        return pal.Services.RegisteredServices.ToList();
    }

    private static void RemoveStaleServices(
        IReadOnlyCollection<RegisteredServiceInfo>? previouslyRegistered,
        IReadOnlyCollection<ServiceRegistrationInfo> activeServices,
        IPolyInstallPal pal,
        Action<string>? progress)
    {
        if (previouslyRegistered is not { Count: > 0 })
            return;
        if (pal.Services is null)
            throw new PlatformNotSupportedException("Service management is not supported on this platform.");

        var platform = CurrentServicePlatform();
        var activeKeys = activeServices
            .Select(service => ServiceKey(service.Name, service.Scope, platform))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var service in previouslyRegistered)
        {
            if (!service.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase))
                continue;
            if (activeKeys.Contains(ServiceKey(service.Name, service.Scope, service.Platform)))
                continue;

            pal.Services.Remove(service);
            progress?.Invoke($"Removed stale service: {service.Name}");
        }
    }

    private static string ServiceKey(string name, string scope, string platform) =>
        $"{platform}:{NormalizeServiceScope(scope)}:{name}";

    private static string NormalizeServiceScope(string scope) =>
        scope.Equals("machine", StringComparison.OrdinalIgnoreCase) ? "system" : scope;

    private static string CurrentServicePlatform() =>
        OperatingSystem.IsWindows()
            ? "windows"
            : OperatingSystem.IsMacOS()
                ? "macos"
                : "linux";
}

public sealed class InstallOperationOptions
{
    public InstallManifest Manifest { get; init; } = null!;
    public string ExtractRoot { get; init; } = "";
    public string Destination { get; init; } = "";
    public IPolyInstallPal Pal { get; init; } = null!;
    public ExistingInstallInfo? ExistingInstall { get; init; }
    public Action<string>? Progress { get; init; }
    public Action<InstallDirectoryPreparedInfo>? OnInstallDirectoryPrepared { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public sealed record InstallDirectoryPreparedInfo(
    string InstallDirectory,
    bool ExistedBefore,
    bool CreatedInstallDirectory,
    InstallMode Mode);

public sealed record InstallOperationResult(
    string InstallDirectory,
    InstallMode Mode,
    bool ExistedBefore,
    bool CreatedInstallDirectory,
    InstallStateDocument State);

public enum InstallMode
{
    Install,
    Update,
    Repair,
}
