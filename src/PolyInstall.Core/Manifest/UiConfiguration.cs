namespace PolyInstall.Manifest;

public sealed class UiConfiguration
{
    /// <summary>
    /// Installer presentation mode. <c>wizard</c> (default) uses the Avalonia UI stub;
    /// <c>silent</c> selects the UI-less stub. Kept as a string so unknown values produce a
    /// semantic diagnostic rather than a deserialisation crash.
    /// </summary>
    public string Mode { get; set; } = "wizard";

    public string Theme { get; set; } = "system";
    public string? LogoPath { get; set; }
    public List<UiAssetEntry>? Assets { get; set; }
    public List<WizardStep> WizardSteps { get; set; } = [];

    public bool IsSilent =>
        Mode.Trim().Equals("silent", StringComparison.OrdinalIgnoreCase);
}

public sealed class UiAssetEntry
{
    public string Id { get; set; } = "";
    public string Path { get; set; } = "";
}

public sealed class WizardStep
{
    public string Type { get; set; } = "";
    public string? Title { get; set; }
    public string? Source { get; set; }
    public string? DefaultPath { get; set; }
    public bool ShowLogs { get; set; } = true;
}
