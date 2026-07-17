using PolyInstall.Core.Build.Validation;

namespace PolyInstall.Cli.Validation;

public static class DiagnosticFormatter
{
    public static string Format(
        IEnumerable<ManifestDiagnostic> diagnostics,
        string manifestPath,
        string? yamlText = null)
    {
        var all = ManifestDiagnosticPipeline.Prepare(diagnostics, yamlText);
        var sourceLines = SplitLines(yamlText);
        var lines = new List<string>();

        foreach (var diagnostic in all)
        {
            lines.Add($"error[{diagnostic.Code}]: {diagnostic.Message}");

            if (diagnostic.Span is { } span)
            {
                lines.Add($"  --> {manifestPath}:{span.Line}:{span.Column}");
                AppendCaretBlock(lines, sourceLines, span);
            }
            else
            {
                lines.Add($"  --> {manifestPath}");
                if (!string.IsNullOrWhiteSpace(diagnostic.Path))
                    lines.Add($"   = note: at {diagnostic.Path}");
            }

            if (!string.IsNullOrWhiteSpace(diagnostic.Help))
                lines.Add($"   = help: {diagnostic.Help}");

            lines.Add(string.Empty);
        }

        lines.Add($"error: manifest validation failed with {all.Count} error{(all.Count == 1 ? string.Empty : "s")}");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendCaretBlock(List<string> lines, IReadOnlyList<string> sourceLines, SourceSpan span)
    {
        if (span.Line <= 0 || span.Line > sourceLines.Count)
            return;

        var source = sourceLines[span.Line - 1];
        var lineNo = span.Line.ToString();
        var gutter = new string(' ', lineNo.Length);

        lines.Add($"{gutter} |");
        lines.Add($"{lineNo} | {source}");

        var startCol = Math.Max(1, span.Column);
        var endCol = span.EndLine == span.Line
            ? Math.Max(startCol + 1, span.EndColumn)
            : Math.Max(startCol + 1, source.Length + 1);
        var caretStart = Math.Min(startCol - 1, source.Length);
        var caretLen = Math.Max(1, Math.Min(endCol - startCol, Math.Max(1, source.Length - caretStart)));
        lines.Add($"{gutter} | {new string(' ', caretStart)}{new string('^', caretLen)}");
        lines.Add($"{gutter} |");
    }

    private static IReadOnlyList<string> SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<string>();

        return text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }
}
