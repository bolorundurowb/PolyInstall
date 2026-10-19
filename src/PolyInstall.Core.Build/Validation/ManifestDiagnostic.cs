namespace PolyInstall.Core.Build.Validation;

/// <summary>1-based source location within a YAML manifest (end column is exclusive).</summary>
public readonly record struct SourceSpan(int Line, int Column, int EndLine, int EndColumn);

/// <summary>Diagnostic severity.</summary>
public enum DiagnosticSeverity
{
    Error = 0,
    Warning = 1,
}

/// <summary>A machine-readable validation diagnostic emitted while reading a manifest.</summary>
public sealed record ManifestDiagnostic(
    string Code,
    string Message,
    string? Path = null,
    string? Help = null,
    SourceSpan? Span = null,
    DiagnosticSeverity Severity = DiagnosticSeverity.Error);

/// <summary>The complete result of schema and semantic manifest validation.</summary>
public sealed class ManifestValidationResult(IReadOnlyList<ManifestDiagnostic> diagnostics)
{
    public IReadOnlyList<ManifestDiagnostic> Diagnostics { get; } = diagnostics;
    public bool IsValid => Diagnostics.All(d => d.Severity != DiagnosticSeverity.Error);

    public static ManifestValidationResult Success { get; } = new(Array.Empty<ManifestDiagnostic>());
}
