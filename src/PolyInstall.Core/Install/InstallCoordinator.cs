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

        options.CancellationToken.ThrowIfCancellationRequested();
        options.Progress?.Invoke("Run pre-install tasks");
        TaskEngine.RunPhase(options.Manifest.Tasks?.PreInstall, options.Pal);
        options.Progress?.Invoke("Pre-install tasks completed");

        options.CancellationToken.ThrowIfCancellationRequested();
        var payloadFiles = PayloadFileInventory.Enumerate(options.ExtractRoot);
        var newPayloadSet = payloadFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            options.CancellationToken,
            relativePath => options.Progress?.Invoke($"Copy file: {relativePath}"));
        options.Progress?.Invoke(mode == InstallMode.Install ? "Files copied" : "Files updated");

        options.CancellationToken.ThrowIfCancellationRequested();
        options.Progress?.Invoke("Run post-install tasks");
        TaskEngine.RunPhase(options.Manifest.Tasks?.PostInstall, options.Pal);
        options.Progress?.Invoke("Post-install tasks completed");

        if (options.Manifest.FileAssociations is { Count: > 0 } && options.Pal.FileAssociations is not null)
        {
            options.Progress?.Invoke("Register file associations");
            foreach (var assoc in options.Manifest.FileAssociations)
            {
                var info = MapToFileAssociationInfo(assoc, options.Pal);
                options.Pal.FileAssociations.Register(info);
            }
            options.Progress?.Invoke("File associations registered");
        }

        options.CancellationToken.ThrowIfCancellationRequested();
        options.Progress?.Invoke("Write install metadata");
        var state = InstallFinalizer.FinalizeInstall(options.Manifest, dest, payloadFiles);

        if (options.Pal.Path is { AddedPaths.Count: > 0 } pathPal)
        {
            state.AddedToPath = pathPal.AddedPaths.Select(a => a.Path).ToList();
            InstallStateIo.WriteState(dest, state);
            options.Progress?.Invoke("PATH entries recorded");
        }

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
