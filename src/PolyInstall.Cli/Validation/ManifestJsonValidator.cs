using System.Text.Json;
using System.Text.RegularExpressions;
using Json.Schema;
using PolyInstall.Core.Build.Validation;

namespace PolyInstall.Cli.Validation;

/// <summary>
/// Provides JSON Schema validation for manifests.
/// </summary>
public static class ManifestJsonValidator
{
    private static readonly Regex TypeMismatch =
        new(@"Value is ""(?<actual>[^""]+)"" but should be ""(?<expected>[^""]+)""", RegexOptions.Compiled);

    /// <summary>
    /// Validates the manifest JSON against the specified schema and throws an <see cref="InvalidOperationException"/> if validation fails.
    /// </summary>
    /// <param name="manifestJson">The JSON content of the manifest.</param>
    /// <param name="schemaPath">The path to the JSON Schema file.</param>
    /// <exception cref="InvalidOperationException">Thrown if validation fails.</exception>
    public static void Validate(string manifestJson, string schemaPath)
    {
        var validation = ValidateResult(manifestJson, schemaPath);
        if (validation.IsValid)
            return;
        throw new InvalidOperationException($"Manifest failed JSON Schema validation:{Environment.NewLine}" +
                                            string.Join(Environment.NewLine, validation.Diagnostics.Select(d => d.Message)));
    }

    /// <summary>
    /// Validates the manifest JSON against the specified schema and returns a <see cref="ManifestValidationResult"/>.
    /// </summary>
    /// <param name="manifestJson">The JSON content of the manifest.</param>
    /// <param name="schemaPath">The path to the JSON Schema file.</param>
    /// <returns>The validation result.</returns>
    public static ManifestValidationResult ValidateResult(string manifestJson, string schemaPath)
    {
        if (string.IsNullOrWhiteSpace(manifestJson))
            return new ManifestValidationResult([
                new("PI001", "Manifest JSON is empty.", "/", "Provide a non-empty manifest document."),
            ]);

        var schemaText = File.ReadAllText(schemaPath);
        var schema = JsonSchema.FromText(schemaText);
        using var instance = JsonDocument.Parse(manifestJson);
        var result = schema.Evaluate(instance.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical });
        if (result.IsValid)
            return ManifestValidationResult.Success;

        var errors = CollectErrors(result)
            .GroupBy(d => (d.Code, d.Path, d.Message), EqualityComparer<(string, string?, string)>.Default)
            .Select(g => g.First())
            .ToList();
        return new ManifestValidationResult(errors.Count > 0
            ? errors
            : [new("PI001", "Schema validation failed.", "/")]);
    }

    private static IEnumerable<ManifestDiagnostic> CollectErrors(EvaluationResults result)
    {
        if (result.Errors is not null)
            foreach (var error in result.Errors)
                yield return CreateDiagnostic(error.Key, error.Value, result.InstanceLocation.ToString());

        if (result.Details is null)
            yield break;

        foreach (var detail in result.Details)
            foreach (var error in CollectErrors(detail))
                yield return error;
    }

    private static ManifestDiagnostic CreateDiagnostic(string keyword, string message, string path)
    {
        path = string.IsNullOrEmpty(path) ? "/" : path;
        var (code, rewritten, help) = keyword switch
        {
            "required" => ("PI002",
                $"required property is missing at '{path}'",
                "Add the required property at this location."),
            "enum" => ("PI003",
                $"value at '{path}' is not one of the allowed enum values",
                "Use one of the values allowed by the manifest schema."),
            "type" => ("PI004", RewriteTypeMessage(message, path),
                "Use the value type required by the manifest schema."),
            "additionalProperties" => ("PI005",
                $"unknown property at '{path}'",
                "Remove the unknown property or correct its spelling."),
            _ when message.Contains("false schema", StringComparison.OrdinalIgnoreCase)
                => ("PI005", $"unknown property at '{path}'",
                    "Remove the unknown property or correct its spelling."),
            _ => ("PI001", message, (string?)null),
        };

        return new ManifestDiagnostic(code, rewritten, path, help);
    }

    private static string RewriteTypeMessage(string message, string path)
    {
        var match = TypeMismatch.Match(message);
        if (match.Success)
            return $"expected {match.Groups["expected"].Value} at '{path}', got {match.Groups["actual"].Value}";
        return $"invalid type at '{path}': {message}";
    }
}
