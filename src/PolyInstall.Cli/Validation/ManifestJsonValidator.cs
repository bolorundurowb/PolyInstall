using System.Text.Json.Nodes;
using Json.Schema;

namespace PolyInstall.Cli.Validation;

public static class ManifestJsonValidator
{
    public static void Validate(string manifestJson, string schemaPath)
    {
        var schemaText = File.ReadAllText(schemaPath);
        var schema = JsonSchema.FromText(schemaText);
        var instance = JsonNode.Parse(manifestJson) ?? throw new InvalidOperationException("Manifest JSON is empty.");
        var result = schema.Evaluate(instance, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        if (result.IsValid)
            return;

        var errors = result.Details
            .Where(d => d.HasErrors)
            .SelectMany(d => d.Errors ?? (IReadOnlyDictionary<string, string>)new Dictionary<string, string>())
            .Select(kv => $"{kv.Key}: {kv.Value}")
            .ToList();
        var msg = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : "Schema validation failed.";
        throw new InvalidOperationException($"Manifest failed JSON Schema validation:{Environment.NewLine}{msg}");
    }
}
