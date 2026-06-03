using PolyInstall.Core.Hosting;
using PolyInstall.Core.Manifest;
using PolyInstall.Core.Pal;

namespace PolyInstall.Core.Install;

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

        options.CancellationToken.ThrowIfCancellationRequested();
        options.Progress?.Invoke("Write install metadata");
        var state = InstallFinalizer.FinalizeInstall(options.Manifest, dest, payloadFiles);
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
