using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Defines the user interface configuration for the installer.
/// </summary>
public sealed class UiConfiguration
{
    /// <summary>Gets or sets the theme for the installer UI (e.g., "system", "light", "dark").</summary>
    [Description("Theme for the installer UI (e.g., 'system', 'light', 'dark').")]
    public string Theme { get; set; } = "system";

    /// <summary>Gets or sets the path to the logo image file, relative to the payload.</summary>
    [Description("Path to the logo image file, relative to the payload.")]
    public string? LogoPath { get; set; }

    /// <summary>Gets or sets the list of UI assets (e.g., license files, images).</summary>
    [Description("List of UI assets (e.g., license files, images).")]
    public List<UiAssetEntry>? Assets { get; set; }

    /// <summary>Gets or sets the sequence of steps in the installation wizard.</summary>
    [Description("Sequence of steps in the installation wizard.")]
    public List<WizardStep> WizardSteps { get; set; } = [];
}

/// <summary>
/// Defines a single UI asset entry.
/// </summary>
public sealed class UiAssetEntry
{
    /// <summary>Gets or sets the identifier for the asset.</summary>
    [Description("Identifier for the asset.")]
    public string Id { get; set; } = "";

    /// <summary>Gets or sets the path to the asset file, relative to the payload.</summary>
    [Description("Path to the asset file, relative to the payload.")]
    public string Path { get; set; } = "";
}

/// <summary>
/// Defines a single step in the installation wizard.
/// </summary>
public sealed class WizardStep
{
    /// <summary>Gets or sets the type of the wizard step (e.g., "welcome", "license", "features", "install").</summary>
    [Description("Type of the wizard step (e.g., 'welcome', 'license', 'features', 'install').")]
    public string Type { get; set; } = "";

    /// <summary>Gets or sets the title shown at the top of the wizard step.</summary>
    [Description("Title shown at the top of the wizard step.")]
    public string? Title { get; set; }

    /// <summary>Gets or sets the source content for the step (e.g., an asset ID for a license step).</summary>
    [Description("Source content for the step (e.g., an asset ID for a license step).")]
    public string? Source { get; set; }

    /// <summary>Gets or sets the default path for path selection steps.</summary>
    [Description("Default path for path selection steps.")]
    public string? DefaultPath { get; set; }

    /// <summary>Gets or sets a value indicating whether to show logs during the progress step. Defaults to true.</summary>
    [Description("Whether to show logs during the progress step. Defaults to true.")]
    public bool ShowLogs { get; set; } = true;
}
