namespace PolyInstall.Core.Build.Validation;

/// <summary>1-based source location within a YAML manifest (end column is exclusive).</summary>
public readonly record struct SourceSpan(int Line, int Column, int EndLine, int EndColumn);

/// <summary>Diagnostic severity. Only errors are emitted today; warnings are reserved for later.</summary>
public enum DiagnosticSeverity
{
    Error = 0,
}

/// <summary>A machine-readable validation error emitted while reading a manifest.</summary>
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
    public bool IsValid => Diagnostics.Count == 0;

    public static ManifestValidationResult Success { get; } = new(Array.Empty<ManifestDiagnostic>());
}
