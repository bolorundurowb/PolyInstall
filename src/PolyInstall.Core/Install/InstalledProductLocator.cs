using PolyInstall.Core.Manifest;

namespace PolyInstall.Core.Install;

public static class InstalledProductLocator
{
    public static ExistingInstallInfo? Find(InstallManifest manifest, IInstallPathPal pal, params string[] candidateInstallDirectories)
    {
        var productId = ProductIdHelper.StableProductGuidString(manifest.Metadata);

        if (OperatingSystem.IsWindows())
        {
#pragma warning disable CA1416 // Guarded by OperatingSystem.IsWindows()
            var arp = FindWindowsArpInstall(productId);
#pragma warning restore CA1416
            if (arp is not null)
                return arp;
        }

        foreach (var candidate in CandidateDirectories(manifest, pal, candidateInstallDirectories))
        {
            var fromState = TryReadFromInstallDirectory(manifest, candidate);
            if (fromState is not null)
                return fromState;
        }

        return null;
    }

    public static ExistingInstallInfo? TryReadFromInstallDirectory(InstallManifest manifest, string installDirectory)
    {
        if (string.IsNullOrWhiteSpace(installDirectory))
            return null;

        InstallStateDocument state;
        try
        {
            if (!File.Exists(InstallStatePaths.InstallStatePath(installDirectory)))
                return null;
            state = InstallStateIo.ReadState(installDirectory);
        }
        catch
        {
            return null;
        }

        var expectedProductId = ProductIdHelper.StableProductGuidString(manifest.Metadata);
        if (!state.ProductId.Equals(expectedProductId, StringComparison.OrdinalIgnoreCase))
            return null;

        return FromState(state, ExistingInstallSource.InstallState);
    }

    private static IEnumerable<string> CandidateDirectories(
        InstallManifest manifest,
        IInstallPathPal pal,
        IEnumerable<string> explicitCandidates)
    {
        foreach (var candidate in explicitCandidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                yield return candidate;
        }

        yield return InstallPathResolver.Expand(DefaultInstallPathResolver.GetDefaultInstallPath(manifest, pal), pal);
    }

    private static ExistingInstallInfo FromState(InstallStateDocument state, ExistingInstallSource source) =>
        new()
        {
            ProductId = state.ProductId,
            DisplayName = state.DisplayName,
            DisplayVersion = state.DisplayVersion,
            Publisher = state.Publisher,
            InstallLocation = state.InstallLocation,
            InstallScope = state.InstallScope,
            Source = source,
            State = state,
        };

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static ExistingInstallInfo? FindWindowsArpInstall(string productId)
    {
        foreach (var scope in new[] { "user", "machine" })
        {
            var arpState = WindowsArpRegistration.TryRead(productId, scope);
            if (arpState is null)
                continue;

            var stateFromDisk = TryReadMatchingStateAtLocation(productId, arpState.InstallLocation, out var stateFileExists);
            if (stateFromDisk is not null)
                return FromState(stateFromDisk, ExistingInstallSource.WindowsArp);
            if (stateFileExists)
                continue;

            return FromState(arpState, ExistingInstallSource.WindowsArp);
        }

        return null;
    }

    private static InstallStateDocument? TryReadMatchingStateAtLocation(
        string productId,
        string installLocation,
        out bool stateFileExists)
    {
        stateFileExists = false;
        if (string.IsNullOrWhiteSpace(installLocation))
            return null;

        try
        {
            if (!File.Exists(InstallStatePaths.InstallStatePath(installLocation)))
                return null;
            stateFileExists = true;
            var state = InstallStateIo.ReadState(installLocation);
            return state.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase)
                ? state
                : null;
        }
        catch
        {
            return null;
        }
    }
}
