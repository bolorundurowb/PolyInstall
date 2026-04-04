using System.Linq;

namespace PolyInstall.Core.Build;

public static class RidMapping
{
    private static readonly IReadOnlyDictionary<string, string> ManifestToDotNetRid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["windows-x64"] = "win-x64",
        ["windows-arm64"] = "win-arm64",
        ["linux-x64"] = "linux-x64",
        ["linux-arm64"] = "linux-arm64",
        ["osx-x64"] = "osx-x64",
        ["osx-arm64"] = "osx-arm64",
    };

    public static string ToDotNetRid(string manifestToken)
    {
        if (ManifestToDotNetRid.TryGetValue(manifestToken.Trim(), out var rid))
            return rid;
        throw new ArgumentException($"Unknown build target RID token: '{manifestToken}'.", nameof(manifestToken));
    }

    public static IReadOnlyCollection<string> KnownTokens => ManifestToDotNetRid.Keys.ToList();
}
