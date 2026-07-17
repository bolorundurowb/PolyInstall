using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace PolyInstall.Core.Build.Validation;

/// <summary>
/// Maps manifest paths (dotted <c>files[0].source_dir</c> or JSON Pointer <c>/files/0/source_dir</c>)
/// to YAML source spans for rustc-style diagnostics.
/// </summary>
public sealed class YamlSourceIndex
{
    private static readonly Regex BracketIndex = new(@"\[(\d+)\]", RegexOptions.Compiled);

    private readonly Dictionary<string, SourceSpan> _spans;

    private YamlSourceIndex(Dictionary<string, SourceSpan> spans)
    {
        _spans = spans;
    }

    public static YamlSourceIndex Build(string yaml)
    {
        var spans = new Dictionary<string, SourceSpan>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(yaml))
            return new YamlSourceIndex(spans);

        var stream = new YamlStream();
        using var reader = new StringReader(yaml);
        stream.Load(reader);
        if (stream.Documents.Count == 0)
            return new YamlSourceIndex(spans);

        IndexNode(stream.Documents[0].RootNode, "/", spans);
        return new YamlSourceIndex(spans);
    }

    public bool TryGet(string? path, out SourceSpan span)
    {
        span = default;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var key = Normalize(path);
        return _spans.TryGetValue(key, out span);
    }

    public static string Normalize(string path)
    {
        path = path.Trim();
        if (path is "/" or "")
            return "/";

        if (path.StartsWith('/'))
        {
            // Already JSON Pointer-ish: /files/0/source_dir
            return path.TrimEnd('/');
        }

        // Dotted with optional [n]: files[0].source_dir / tasks.post_install[0].parameters
        var withSlashes = BracketIndex.Replace(path, "/$1");
        var parts = withSlashes.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return "/" + string.Join('/', parts);
    }

    private static void IndexNode(YamlNode node, string path, Dictionary<string, SourceSpan> spans)
    {
        spans[path] = FromMarks(node.Start, node.End);

        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var (keyNode, valueNode) in mapping.Children)
                {
                    if (keyNode is not YamlScalarNode keyScalar)
                        continue;
                    var childPath = Append(path, keyScalar.Value ?? string.Empty);
                    spans[childPath] = FromMarks(keyScalar.Start, valueNode.End);
                    IndexNode(valueNode, childPath, spans);
                }
                break;

            case YamlSequenceNode sequence:
                var index = 0;
                foreach (var child in sequence.Children)
                {
                    var childPath = Append(path, index.ToString());
                    IndexNode(child, childPath, spans);
                    index++;
                }
                break;
        }
    }

    private static string Append(string parent, string segment)
    {
        if (parent == "/")
            return "/" + segment;
        return parent + "/" + segment;
    }

    private static SourceSpan FromMarks(YamlDotNet.Core.Mark start, YamlDotNet.Core.Mark end)
    {
        var endLine = (int)end.Line;
        var endColumn = (int)end.Column;
        var startLine = (int)start.Line;
        var startColumn = (int)start.Column;
        if (endLine < startLine || (endLine == startLine && endColumn <= startColumn))
        {
            endLine = startLine;
            endColumn = startColumn + 1;
        }

        return new SourceSpan(startLine, startColumn, endLine, endColumn);
    }
}

/// <summary>Merges diagnostics, attaches YAML spans when available, and sorts for display.</summary>
public static class ManifestDiagnosticPipeline
{
    public static IReadOnlyList<ManifestDiagnostic> Prepare(
        IEnumerable<ManifestDiagnostic> diagnostics,
        string? yamlText)
    {
        YamlSourceIndex? index = null;
        if (!string.IsNullOrWhiteSpace(yamlText))
        {
            try
            {
                index = YamlSourceIndex.Build(yamlText);
            }
            catch
            {
                // Never drop diagnostics because indexing failed.
                index = null;
            }
        }

        var prepared = diagnostics
            .Select(d =>
            {
                if (d.Span is not null || d.Path is null || index is null)
                    return d;
                return index.TryGet(d.Path, out var span) ? d with { Span = span } : d;
            })
            .OrderBy(d => d.Span?.Line ?? int.MaxValue)
            .ThenBy(d => d.Span?.Column ?? int.MaxValue)
            .ThenBy(d => d.Path ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(d => d.Code, StringComparer.Ordinal)
            .ToList();

        return prepared;
    }
}
