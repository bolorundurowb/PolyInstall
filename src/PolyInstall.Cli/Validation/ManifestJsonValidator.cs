using System.Text.Json;
using Json.Schema;

namespace PolyInstall.Cli.Validation;

public static class ManifestJsonValidator
{
    public static void Validate(string manifestJson, string schemaPath)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
            throw new InvalidOperationException("Manifest JSON is empty.");

        var schemaText = File.ReadAllText(schemaPath);
        var schema = JsonSchema.FromText(schemaText);
        using var instance = JsonDocument.Parse(manifestJson);
        var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        if (result.IsValid)
            return;

        var errors = CollectErrors(result).ToList();
        var msg = errors.Count > 0 ? string.Join(Environment.NewLine, errors) : "Schema validation failed.";
        throw new InvalidOperationException($"Manifest failed JSON Schema validation:{Environment.NewLine}{msg}");
    }

    private static IEnumerable<string> CollectErrors(EvaluationResults result)
    {
        if (result.Errors is not null)
        {
            foreach (var error in result.Errors)
                yield return $"{error.Key}: {error.Value}";
        }

        if (result.Details is null)
            yield break;

        foreach (var detail in result.Details)
        {
            foreach (var error in CollectErrors(detail))
                yield return error;
        }
    }
}
