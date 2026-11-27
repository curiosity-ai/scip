using System.Text;
using ScipSharp.Indexer.Delphi.Project;
using ScipSharp.Indexer.Delphi.Syntax;

namespace ScipSharp.Indexer.Delphi.Tests;

internal static class TestUtil
{
    public static string TestData => Path.Combine(AppContext.BaseDirectory, "testdata");

    public static FileSyntax Parse(string source, CompilerSettings? settings = null)
    {
        var raw = Lexer.Tokenize(source, 0);
        var tokens = new Preprocessor(settings ?? CompilerSettings.Default, static (_, _) => null).Process(raw, "test.pas", 0);
        return Parser.ParseFile(tokens);
    }

    public static string NewProject(Dictionary<string, string> files) =>
        NewProject(files.ToDictionary(kv => kv.Key, kv => Encoding.UTF8.GetBytes(kv.Value)));

    public static string NewProject(Dictionary<string, byte[]> files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-delphi-" + Guid.NewGuid().ToString("N"));
        foreach (var (name, content) in files)
        {
            var p = Path.Combine(dir, name);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllBytes(p, content);
        }

        return dir;
    }

    public static Index Index(string dir, DelphiIndexerOptions? options = null, List<string>? warnings = null) =>
        new DelphiProjectIndexer(dir, options ?? new DelphiIndexerOptions(), [], warnings is null ? null : warnings.Add).Build();

    public static Index Index(Dictionary<string, string> files, DelphiIndexerOptions? options = null) => Index(NewProject(files), options);

    /// <summary>Symbols of all occurrences in <paramref name="path"/> whose text is <paramref name="text"/>.</summary>
    public static List<(string Symbol, int Roles)> OccurrencesOf(Index index, string path, string sourceText, string text)
    {
        var doc = index.Documents.Single(d => d.RelativePath == path);
        var lines = sourceText.Split('\n');
        var result = new List<(string, int)>();
        foreach (var o in doc.Occurrences)
        {
            var r = o.GetSourceRange()!.Value;
            if (r.Start.Line == r.End.Line && r.Start.Line < lines.Length)
            {
                var line = lines[r.Start.Line];
                if (r.End.Character <= line.Length && line[r.Start.Character..r.End.Character] == text)
                {
                    result.Add((o.Symbol, o.SymbolRoles));
                }
            }
        }

        return result;
    }

    /// <summary>The checks of <c>scip lint</c> that matter for an indexer (port of the relevant parts of IndexLinter).</summary>
    public static List<string> Lint(Index index)
    {
        var errors = new List<string>();
        var external = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in index.ExternalSymbols)
        {
            if (!external.Add(e.Symbol))
            {
                errors.Add("duplicate external " + e.Symbol);
            }

            Check(e.Symbol, "external");
        }

        var documentSymbols = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var d in index.Documents)
        {
            var mine = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in d.Symbols)
            {
                if (!mine.Add(s.Symbol))
                {
                    errors.Add($"duplicate info {s.Symbol} in {d.RelativePath}");
                }

                if (external.Contains(s.Symbol))
                {
                    errors.Add($"both local and external {s.Symbol}");
                }

                Check(s.Symbol, d.RelativePath);
            }

            documentSymbols[d.RelativePath] = mine;
            if (d.PositionEncoding == PositionEncoding.UnspecifiedPositionEncoding)
            {
                errors.Add("no position encoding " + d.RelativePath);
            }
        }

        var allGlobal = documentSymbols.Values.SelectMany(s => s).Where(s => !s.StartsWith("local ", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
        foreach (var d in index.Documents)
        {
            foreach (var s in d.Symbols)
            {
                foreach (var r in s.Relationships)
                {
                    if (!allGlobal.Contains(r.Symbol) && !external.Contains(r.Symbol))
                    {
                        errors.Add($"relationship target without info {r.Symbol}");
                    }
                }
            }

            var seen = new HashSet<(Range, int, string)>();
            foreach (var o in d.Occurrences)
            {
                var r = o.GetSourceRange();
                if (r is null)
                {
                    errors.Add("no range");
                    continue;
                }

                if (!seen.Add((r.Value, o.SymbolRoles, o.Symbol)))
                {
                    errors.Add($"duplicate occurrence {o.Symbol} at {r} in {d.RelativePath}");
                }

                var isLocal = o.Symbol.StartsWith("local ", StringComparison.Ordinal);
                if (isLocal ? !documentSymbols[d.RelativePath].Contains(o.Symbol) : !allGlobal.Contains(o.Symbol) && !external.Contains(o.Symbol))
                {
                    errors.Add($"no info for {o.Symbol} in {d.RelativePath}");
                }

                if ((o.SymbolRoles & (int)SymbolRole.Definition) != 0 && (o.SymbolRoles & (int)SymbolRole.ForwardDefinition) != 0)
                {
                    errors.Add("forward definition is definition " + o.Symbol);
                }

                if (o.GetEnclosingSourceRange() is { } enc && (enc.Start > r.Value.Start || enc.End < r.Value.End))
                {
                    errors.Add($"enclosing range does not enclose {o.Symbol} in {d.RelativePath}");
                }

                Check(o.Symbol, d.RelativePath);
            }
        }

        return errors;

        void Check(string symbol, string where)
        {
            if (symbol.StartsWith("local ", StringComparison.Ordinal))
            {
                return;
            }

            if (!SymbolParser.TryParse(symbol, out var parsed, out var err))
            {
                errors.Add($"invalid symbol in {where}: {symbol}: {err}");
                return;
            }

            if (SymbolFormatter.Verbose.Format(parsed) != symbol)
            {
                errors.Add($"non-canonical symbol in {where}: {symbol}");
            }
        }
    }
}
