using System.ComponentModel;

namespace PolyInstall.Manifest;

/// <summary>
/// Build-time mapping that records, for every file in the payload zip, whether it is part
/// of the always-installed core or belongs to one or more optional features. Embedded in
/// the manifest JSON so the runtime stub can filter payload extraction at install time.
/// </summary>
public sealed class PayloadFeatureIndex
{
    [Description("Relative payload paths that are always installed regardless of selected features.")]
    public List<string> CoreFiles { get; set; } = [];

    [Description("Map of feature id → relative payload paths that belong to that feature. A file may appear under multiple features.")]
    public Dictionary<string, List<string>> FeatureFiles { get; set; } = new();
}
