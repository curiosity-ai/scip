using System.Text;
using ScipSharp.Indexer.Vb6.Text;

namespace ScipSharp.Indexer.Vb6.Tests;

internal static class TestSupport
{
    /// <summary>
    /// The checked-in testdata directory (so that <c>SCIP_UPDATE_SNAPSHOTS=1</c> rewrites the committed snapshots), or the
    /// copy next to the test assembly when the sources are not reachable.
    /// </summary>
    public static string TestData { get; } = FindTestData();

    private static string FindTestData()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "tests", "ScipSharp.Indexer.Vb6.Tests", "testdata");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "testdata");
    }

    public static Index Build(string dir, Vb6Options? options = null, List<string>? warnings = null) =>
        new Vb6ProjectIndexer(dir, options, warnings is null ? null : warnings.Add).Build();

    /// <summary>
    /// The checks of <c>scip lint</c>: canonical, parseable symbols; exactly one SymbolInformation per symbol and document;
    /// SymbolInformation for every occurrence; relationship targets that exist; no duplicate occurrences or documents.
    /// </summary>
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
        }

        var known = new HashSet<string>(external, StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in index.Documents)
        {
            if (!paths.Add(d.RelativePath))
            {
                errors.Add("duplicate document " + d.RelativePath);
            }

            var inDoc = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in d.Symbols)
            {
                if (!inDoc.Add(s.Symbol))
                {
                    errors.Add($"{d.RelativePath}: duplicate info {s.Symbol}");
                }

                if (external.Contains(s.Symbol))
                {
                    errors.Add($"{d.RelativePath}: both local and external {s.Symbol}");
                }

                CheckSymbol(s.Symbol, errors);
                known.Add(d.RelativePath + "\0" + s.Symbol);
                if (!s.Symbol.StartsWith("local ", StringComparison.Ordinal))
                {
                    known.Add(s.Symbol);
                }
            }
        }

        foreach (var d in index.Documents)
        {
            foreach (var s in d.Symbols)
            {
                foreach (var r in s.Relationships)
                {
                    if (!known.Contains(r.Symbol) && !known.Contains(d.RelativePath + "\0" + r.Symbol))
                    {
                        errors.Add($"{d.RelativePath}: relationship of {s.Symbol} to unknown {r.Symbol}");
                    }
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var o in d.Occurrences)
            {
                if (!o.TryGetSourceRange(out var range) || range.Validate() is not null)
                {
                    errors.Add($"{d.RelativePath}: bad range for {o.Symbol}");
                    continue;
                }

                CheckSymbol(o.Symbol, errors);
                var local = o.Symbol.StartsWith("local ", StringComparison.Ordinal);
                if (!(local ? known.Contains(d.RelativePath + "\0" + o.Symbol) : known.Contains(o.Symbol)))
                {
                    errors.Add($"{d.RelativePath}: no SymbolInformation for occurrence {o.Symbol} at {range}");
                }

                if (!seen.Add($"{o.Symbol}|{range}|{o.SymbolRoles}"))
                {
                    errors.Add($"{d.RelativePath}: duplicate occurrence {o.Symbol} at {range}");
                }
            }
        }

        return errors;
    }

    private static void CheckSymbol(string symbol, List<string> errors)
    {
        if (!SymbolParser.TryParse(symbol, out var parsed, out var error))
        {
            errors.Add($"invalid symbol '{symbol}': {error}");
            return;
        }

        var canonical = SymbolFormatter.Verbose.Format(parsed);
        if (canonical != symbol)
        {
            errors.Add($"non-canonical symbol '{symbol}' (expected '{canonical}')");
        }
    }

    /// <summary>
    /// Renders a document in <c>scip snapshot</c> format. The source is decoded like the indexer does (UTF-8 or Windows-1252)
    /// so that the rendered columns are the UTF-16 offsets of the index.
    /// </summary>
    public static string Snapshot(Document doc, string root)
    {
        var source = SourceText.Read(Path.Combine(root, doc.RelativePath), SourceText.Windows1252);
        var temp = Path.Combine(Path.GetTempPath(), $"scip-vb6-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(temp, source.Text, new UTF8Encoding(false));
            var formatted = SnapshotFormatter.FormatSnapshot(doc.Clone(), "'", SymbolFormatter.Verbose, temp);
            if (formatted.Errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", formatted.Errors.Select(e => e.Message)));
            }

            return formatted.Text;
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public static string ExternalSymbols(Index index)
    {
        var b = new StringBuilder();
        foreach (var s in index.ExternalSymbols.OrderBy(s => s.Symbol, StringComparer.Ordinal))
        {
            b.Append(s.Symbol).Append('\n');
            b.Append("  kind ").Append(s.Kind).Append('\n');
            if (s.SignatureDocumentation is { Text.Length: > 0 } sig)
            {
                b.Append("  signature ").Append(sig.Text.Replace("\n", "\n            ", StringComparison.Ordinal)).Append('\n');
            }
        }

        return b.ToString();
    }

    /// <summary>Writes <paramref name="files"/> (path → text, '\n' becomes CRLF) into a fresh temp directory.</summary>
    public static TempDir Files(params (string Path, string Text)[] files)
    {
        var dir = new TempDir();
        foreach (var (path, text) in files)
        {
            var full = Path.Combine(dir.Path, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllBytes(full, SourceText.Windows1252.GetBytes(text.ReplaceLineEndings("\r\n")));
        }

        return dir;
    }
}

internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"scip-vb6-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
