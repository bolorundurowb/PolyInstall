namespace PolyInstall.Manifest;

public sealed class UiConfiguration
{
    public string Theme { get; set; } = "system";
    public string? LogoPath { get; set; }
    public List<UiAssetEntry>? Assets { get; set; }
    public List<WizardStep> WizardSteps { get; set; } = [];
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
}
