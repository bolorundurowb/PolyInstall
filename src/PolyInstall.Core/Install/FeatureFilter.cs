using PolyInstall.Manifest;

namespace PolyInstall.Install;

/// <summary>
/// Helpers that decide which payload files, tasks, and file associations are active
/// for a given set of selected features.
/// </summary>
public static class FeatureFilter
{
    /// <summary>
    /// Builds the set of payload-relative paths that should be installed. A file is allowed when:
    ///  • the manifest has no <see cref="PayloadFeatureIndex"/> (backward compat → install all), or
    ///  • the file is in <see cref="PayloadFeatureIndex.CoreFiles"/>, or
    ///  • the file is not referenced by any feature in <see cref="PayloadFeatureIndex.FeatureFiles"/>
    ///    (treated as core for forward compat), or
    ///  • the file is in <see cref="PayloadFeatureIndex.FeatureFiles"/> for at least one selected feature.
    /// All paths are normalized to forward slashes (matching <see cref="PayloadFileInventory.NormalizeRelativePath"/>).
    /// </summary>
    public static IReadOnlySet<string> ComputeAllowedFiles(
        PayloadFeatureIndex? index,
        IEnumerable<string> payloadFiles,
        IReadOnlySet<string> selectedFeatures)
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (index is null)
        {
            foreach (var f in payloadFiles)
                allowed.Add(PayloadFileInventory.NormalizeRelativePath(f));
            return allowed;
        }

        var fileToFeatures = BuildFileToFeatures(index);

        foreach (var raw in payloadFiles)
        {
            var f = PayloadFileInventory.NormalizeRelativePath(raw);
            if (!fileToFeatures.TryGetValue(f, out var gates))
            {
                allowed.Add(f);
                continue;
            }

            foreach (var gate in gates)
            {
                if (selectedFeatures.Contains(gate))
                {
                    allowed.Add(f);
                    break;
                }
            }
        }

        return allowed;
    }

    /// <summary>
    /// Returns true when an entry that lists <paramref name="entryFeatures"/> should run given the
    /// current <paramref name="selectedFeatures"/>. Null/empty feature lists always run.
    /// </summary>
    public static bool IsActive(IReadOnlyCollection<string>? entryFeatures, IReadOnlySet<string> selectedFeatures)
    {
        if (entryFeatures is null || entryFeatures.Count == 0)
            return true;
        foreach (var fid in entryFeatures)
        {
            if (selectedFeatures.Contains(fid))
                return true;
        }
        return false;
    }

    private static Dictionary<string, HashSet<string>> BuildFileToFeatures(PayloadFeatureIndex index)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (featureId, files) in index.FeatureFiles)
        {
            if (string.IsNullOrWhiteSpace(featureId) || files is null)
                continue;
            foreach (var raw in files)
            {
                var f = PayloadFileInventory.NormalizeRelativePath(raw);
                if (!map.TryGetValue(f, out var set))
                    map[f] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                set.Add(featureId);
            }
        }

        // CoreFiles override: explicit core takes precedence even if accidentally listed in a feature.
        foreach (var raw in index.CoreFiles)
        {
            var f = PayloadFileInventory.NormalizeRelativePath(raw);
            map.Remove(f);
        }

        return map;
    }
}
