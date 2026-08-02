namespace PolyInstall.Build;

/// <summary>
/// Provides mapping between manifest target tokens and .NET Runtime Identifiers (RIDs).
/// </summary>
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

    /// <summary>
    /// Converts a manifest target token to its corresponding .NET RID.
    /// </summary>
    /// <param name="manifestToken">The manifest target token (e.g., "windows-x64").</param>
    /// <returns>The corresponding .NET RID.</returns>
    /// <exception cref="ArgumentException">Thrown if the token is unknown.</exception>
    public static string ToDotNetRid(string manifestToken)
    {
        if (ManifestToDotNetRid.TryGetValue(manifestToken.Trim(), out var rid))
            return rid;
        throw new ArgumentException($"Unknown build target RID token: '{manifestToken}'.", nameof(manifestToken));
    }
}
