using PolyInstall.Install;
using PolyInstall.Manifest;
using PolyInstall.Pal;

namespace PolyInstall.Hosting;

/// <summary>
/// Populated by the runtime host before Avalonia starts; read by the UI layer.
/// </summary>
public static class InstallBootstrap
{
    /// <summary>Gets the installation manifest.</summary>
    public static InstallManifest Manifest { get; private set; } = null!;

    /// <summary>Gets the root directory where the payload has been extracted.</summary>
    public static string ExtractRoot { get; private set; } = "";

    /// <summary>Gets the platform abstraction layer.</summary>
    public static IPolyInstallPal Pal { get; private set; } = null!;

    /// <summary>Gets or sets information about an existing installation, if any.</summary>
    public static ExistingInstallInfo? ExistingInstall { get; set; }

    /// <summary>Gets or sets the selected installation mode.</summary>
    public static InstallMode SelectedInstallMode { get; set; } = InstallMode.Install;

    /// <summary>Gets or sets the target installation directory.</summary>
    public static string? InstallDirectory { get; set; }

    /// <summary>
    /// Gets or sets the feature identifiers selected for this installation run.
    /// Populated by the UI's features step or pre-seeded for update/repair.
    /// </summary>
    public static HashSet<string> SelectedFeatures { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes the bootstrap state with the specified parameters.
    /// </summary>
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
