using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace PolyInstall.Core.Manifest;

/// <summary>
/// Substitutes <c>${VAR}</c> and <c>${VAR:-default}</c> in all string values of a JSON document (CLI build-time only).
/// </summary>
public static class EnvironmentSubstitution
{
    private static readonly Regex Pattern = new(
        @"\$\{(?<name>[A-Za-z_][A-Za-z0-9_]*)(?::-(?<def>[^}]*))?\}",
        RegexOptions.Compiled);

    public static string ApplyToJson(string json, IReadOnlyDictionary<string, string>? extra = null)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidOperationException("Invalid JSON.");
        Walk(node, extra ?? ReadOnlyDictionary<string, string>.Empty);
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public static void Walk(JsonNode? node, IReadOnlyDictionary<string, string> extra)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kv in obj.ToList())
                {
                    if (kv.Value is JsonValue jv && jv.TryGetValue<string>(out var s))
                        obj[kv.Key] = Substitute(s, extra);
                    else
                        Walk(kv.Value, extra);
                }
                break;
            case JsonArray arr:
                for (var i = 0; i < arr.Count; i++)
                {
                    if (arr[i] is JsonValue jv && jv.TryGetValue<string>(out var s))
                        arr[i] = Substitute(s, extra);
                    else
                        Walk(arr[i], extra);
                }
                break;
        }
    }

    public static string Substitute(string input, IReadOnlyDictionary<string, string> extra)
    {
        return Pattern.Replace(input, m =>
        {
            var name = m.Groups["name"].Value;
            var defGroup = m.Groups["def"];
            var hasDef = defGroup.Success;
            if (extra.TryGetValue(name, out var v) && v.Length > 0)
                return v;
            var env = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(env))
                return env;
            if (hasDef)
                return defGroup.Value;
            return m.Value;
        });
    }

    /// <summary>
    /// Walks all string properties of <paramref name="manifest"/> via JSON round-trip substitution.
    /// </summary>
    public static InstallManifest ApplyToManifest(InstallManifest manifest, IReadOnlyDictionary<string, string>? extra = null)
    {
        var json = JsonSerializer.Serialize(manifest, InstallManifest.JsonOptions);
        json = ApplyToJson(json, extra);
        return JsonSerializer.Deserialize<InstallManifest>(json, InstallManifest.JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize manifest after substitution.");
    }
}
