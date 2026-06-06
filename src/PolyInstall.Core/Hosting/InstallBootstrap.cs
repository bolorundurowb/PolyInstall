using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Hosting;

/// <summary>
/// Populated by the runtime host before Avalonia starts; read by the UI layer.
/// </summary>
public static class InstallBootstrap
{
    public static InstallManifest Manifest { get; private set; } = null!;
    public static string ExtractRoot { get; private set; } = "";
    public static IPolyInstallPal Pal { get; private set; } = null!;
    public static ExistingInstallInfo? ExistingInstall { get; set; }
    public static InstallMode SelectedInstallMode { get; set; } = InstallMode.Install;
    public static string? InstallDirectory { get; set; }

    /// <summary>
    /// Feature ids selected for this install run. Populated by the UI's <c>features</c> step
    /// (or pre-seeded for update/repair) and consumed by <see cref="InstallCoordinator"/>.
    /// </summary>
    public static HashSet<string> SelectedFeatures { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static void Init(
        InstallManifest manifest,
        string extractRoot,
        IPolyInstallPal pal,
        ExistingInstallInfo? existingInstall = null)
    {
        Manifest = manifest;
        ExtractRoot = extractRoot;
        Pal = pal;
        ExistingInstall = existingInstall;
        SelectedInstallMode = InstallCoordinator.ResolveMode(manifest, existingInstall);
        InstallDirectory = null;
        if (existingInstall is not null)
            InstallDirectory = existingInstall.InstallLocation;

        SelectedFeatures = SeedSelectedFeatures(manifest, existingInstall);
    }

    private static HashSet<string> SeedSelectedFeatures(InstallManifest manifest, ExistingInstallInfo? existingInstall)
    {
        var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (existingInstall?.State?.SelectedFeatures is { Count: > 0 } prior)
        {
            foreach (var id in prior)
            {
                if (!string.IsNullOrWhiteSpace(id))
                    selected.Add(id);
            }
            return selected;
        }

        if (manifest.Features is { Count: > 0 } defined)
        {
            foreach (var feat in defined)
            {
                if (!string.IsNullOrWhiteSpace(feat.Id) && feat.DefaultSelected)
                    selected.Add(feat.Id);
            }
        }

        return selected;
    }
}
