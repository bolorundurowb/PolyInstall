using System.Security.Cryptography;
using System.Text;
using PolyInstall.Manifest;

namespace PolyInstall.Install;

/// <summary>
/// Stable product GUID for ARP keys and install state, derived from manifest metadata.
/// </summary>
public static class ProductIdHelper
{
    /// <summary>Returns a registry-style GUID string with braces, e.g. <c>{XXXXXXXX-...}</c>.</summary>
    public static string StableProductGuidString(ManifestMetadata metadata)
    {
        var id = metadata.Id?.Trim();
        if (!string.IsNullOrEmpty(id))
        {
            if (Guid.TryParse(id, out var parsed))
                return parsed.ToString("B").ToUpperInvariant();
            return GuidFromUtf8String(id).ToString("B").ToUpperInvariant();
        }

        var seed = $"{metadata.Name}\0{metadata.Publisher ?? ""}";
        return GuidFromUtf8String(seed).ToString("B").ToUpperInvariant();
    }

    private static Guid GuidFromUtf8String(string s)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(s));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new Guid(guidBytes);
    }
}
