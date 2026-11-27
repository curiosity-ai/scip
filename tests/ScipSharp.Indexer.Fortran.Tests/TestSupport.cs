using System.Runtime.CompilerServices;
using ScipSharp.Indexer.Fortran.Syntax;

namespace ScipSharp.Indexer.Fortran.Tests;

/// <summary>Helpers shared by the Fortran indexer tests.</summary>
internal static class TestSupport
{
    public static string TestData => Path.Combine(AppContext.BaseDirectory, "testdata");

    /// <summary>The checked-in testdata directory (snapshot updates are written there, not to the build output).</summary>
    public static string SourceTestData([CallerFilePath] string here = "") => Path.Combine(Path.GetDirectoryName(here)!, "testdata");

    /// <summary>Writes <paramref name="files"/> into a fresh temporary project and indexes it.</summary>
    public static Indexed Build(Dictionary<string, string> files, FortranOptions? options = null)
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-fortran-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var (name, content) in files)
            {
                var path = Path.Combine(dir, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, content);
            }

            var indexer = new FortranProjectIndexer(dir, options);
            var index = indexer.Build();
            return new Indexed(index, files, indexer.Unresolved);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>A single-file project.</summary>
    public static Indexed Build(string fileName, string source, FortranOptions? options = null) => Build(new() { [fileName] = source }, options);

    public static Index IndexDirectory(string dir, FortranOptions? options = null) => new FortranProjectIndexer(dir, options).Build();

    public static bool IsDefinition(int roles) => (roles & (int)SymbolRole.Definition) != 0;

    public static SymbolInformation Info(Index index, string symbol) =>
        index.Documents.SelectMany(static d => d.Symbols).Concat(index.ExternalSymbols).First(s => s.Symbol == symbol);

    /// <summary>
    /// The invariants checked by <c>scip lint</c>: canonical symbols, SymbolInformation for every occurrence (locals in
    /// their document), relationships to known symbols, no duplicate occurrences / infos / relationships.
    /// </summary>
    public static List<string> Lint(Index index)
    {
        var errors = new List<string>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ext in index.ExternalSymbols)
        {
            if (!known.Add(ext.Symbol))
            {
                errors.Add("duplicate external " + ext.Symbol);
            }
        }

        var external = known.ToHashSet(StringComparer.Ordinal);
        var relationships = new HashSet<(string, string)>();
        foreach (var doc in index.Documents)
        {
            var perDoc = new HashSet<string>(StringComparer.Ordinal);
            foreach (var info in doc.Symbols)
            {
                if (!perDoc.Add(info.Symbol))
                {
                    errors.Add($"{doc.RelativePath}: duplicate info {info.Symbol}");
                }

                if (external.Contains(info.Symbol))
                {
                    errors.Add($"{doc.RelativePath}: both local and external {info.Symbol}");
                }

                if (!info.Symbol.StartsWith("local ", StringComparison.Ordinal))
                {
                    known.Add(info.Symbol);
                }

                CheckSymbol(info.Symbol, doc.RelativePath, errors);
            }
        }

        foreach (var doc in index.Documents)
        {
            var locals = doc.Symbols.Select(static s => s.Symbol).Where(static s => s.StartsWith("local ", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
            foreach (var info in doc.Symbols)
            {
                foreach (var rel in info.Relationships)
                {
                    if (!known.Contains(rel.Symbol) && !locals.Contains(rel.Symbol))
                    {
                        errors.Add($"{doc.RelativePath}: relationship to unknown {rel.Symbol}");
                    }

                    if (!relationships.Add((info.Symbol.StartsWith("local ", StringComparison.Ordinal) ? doc.RelativePath + info.Symbol : info.Symbol, rel.Symbol)))
                    {
                        errors.Add($"{doc.RelativePath}: repeated relationship {info.Symbol} -> {rel.Symbol}");
                    }
                }
            }

            if (doc.PositionEncoding != PositionEncoding.Utf16CodeUnitOffsetFromLineStart)
            {
                errors.Add($"{doc.RelativePath}: position encoding not set");
            }

            var seen = new HashSet<(string, int, int, int, int, int)>();
            foreach (var occ in doc.Occurrences)
            {
                if (!occ.TryGetSourceRange(out var r))
                {
                    errors.Add($"{doc.RelativePath}: invalid range for {occ.Symbol}");
                    continue;
                }

                CheckSymbol(occ.Symbol, doc.RelativePath, errors);
                var isLocal = occ.Symbol.StartsWith("local ", StringComparison.Ordinal);
                if (isLocal ? !locals.Contains(occ.Symbol) : !known.Contains(occ.Symbol))
                {
                    errors.Add($"{doc.RelativePath}:{r.Start.Line}: no info for {occ.Symbol}");
                }

                if (!seen.Add((occ.Symbol, r.Start.Line, r.Start.Character, r.End.Line, r.End.Character, occ.SymbolRoles)))
                {
                    errors.Add($"{doc.RelativePath}:{r.Start.Line}: duplicate occurrence {occ.Symbol}");
                }

                if (SymbolRole.Definition.Matches(occ) && SymbolRole.ForwardDefinition.Matches(occ))
                {
                    errors.Add($"{doc.RelativePath}:{r.Start.Line}: forward definition is a definition {occ.Symbol}");
                }
            }
        }

        return errors;
    }

    private static void CheckSymbol(string symbol, string where, List<string> errors)
    {
        if (!SymbolParser.TryParse(symbol, out var parsed, out var error))
        {
            errors.Add($"{where}: invalid symbol {symbol}: {error}");
            return;
        }

        var canonical = SymbolFormatter.Verbose.Format(parsed);
        if (canonical != symbol)
        {
            errors.Add($"{where}: non-canonical symbol {symbol} (expected {canonical})");
        }
    }
}

/// <summary>An index plus its sources, with lookups by source text.</summary>
internal sealed class Indexed(Index index, Dictionary<string, string> files, IReadOnlyList<Token> unresolved)
{
    public Index Index { get; } = index;

    public IReadOnlyList<Token> Unresolved { get; } = unresolved;

    public Document Doc(string path) => Index.Documents.Single(d => d.RelativePath == path);

    /// <summary>Occurrences starting at the <paramref name="nth"/> occurrence of <paramref name="token"/> on the first line containing <paramref name="line"/>.</summary>
    public List<Occurrence> At(string path, string line, string token, int nth = 0, bool wholeWord = true)
    {
        var lines = files[path].Split('\n');
        var lineNo = Array.FindIndex(lines, l => l.Contains(line, StringComparison.Ordinal));
        if (lineNo < 0)
        {
            throw new ArgumentException($"no line containing '{line}'");
        }

        // whole-word matches only, so "n" does not match inside "print"
        var text = lines[lineNo];
        static bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
        var col = -1;
        for (var i = 0; i <= nth; i++)
        {
            do
            {
                col = text.IndexOf(token, col + 1, StringComparison.Ordinal);
            }
            while (col >= 0 && wholeWord && IsWord(token[0]) && ((col > 0 && IsWord(text[col - 1])) || (col + token.Length < text.Length && IsWord(text[col + token.Length]))));

            if (col < 0)
            {
                throw new ArgumentException($"'{token}' not found on line {lineNo}");
            }
        }

        return Doc(path).Occurrences.Where(o => o.TryGetSourceRange(out var r) && r.Start.Line == lineNo && r.Start.Character == col).ToList();
    }

    /// <summary>The symbol at a token (null when there is no occurrence).</summary>
    public string? Sym(string path, string line, string token, int nth = 0, bool wholeWord = true) =>
        At(path, line, token, nth, wholeWord).Select(static o => o.Symbol).FirstOrDefault();

    public bool IsDef(string path, string line, string token, int nth = 0) =>
        At(path, line, token, nth).Any(static o => SymbolRole.Definition.Matches(o));

    public SymbolInformation Info(string symbol) => TestSupport.Info(Index, symbol);
}
