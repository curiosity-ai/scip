namespace ScipSharp.Indexer.Cobol.Tests;

/// <summary>A throw-away project directory for end-to-end tests.</summary>
internal sealed class TempProject : IDisposable
{
    public TempProject(Dictionary<string, string> files)
    {
        Root = Path.Combine(Path.GetTempPath(), "scip-cobol-" + Guid.NewGuid().ToString("N"));
        foreach (var (name, content) in files)
        {
            Write(name, content);
        }
    }

    public string Root { get; }

    public void Write(string name, string content)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public Index Index(CobolIndexerOptions? options = null, List<string>? warnings = null) =>
        TestSupport.Index(Root, options ?? new CobolIndexerOptions { PackageName = "t" }, warnings);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>Indents every line to column 8 so snippets can be written as fixed-format area A/B text.</summary>
    public static string Fixed(string text) =>
        string.Join("\n", text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(l => l.Length == 0 ? l : "       " + l)) + "\n";
}

internal static class IndexQueries
{
    public const string Pkg = "scip-cobol cobol t 0.0.0 ";

    public static Document Doc(this Index index, string path) => index.Documents.Single(d => d.RelativePath == path);

    /// <summary>Symbols of the occurrences on a 0-based line, in column order.</summary>
    public static List<string> SymbolsOnLine(this Document doc, int line, bool definitions = false) =>
        doc.Occurrences
            .Where(o => o.TryGetSourceRange(out var r) && r.Start.Line == line && ((o.SymbolRoles & (int)SymbolRole.Definition) != 0) == definitions)
            .OrderBy(o => o.GetSourceRange()!.Value.Start.Character)
            .Select(o => o.Symbol.StartsWith(Pkg, StringComparison.Ordinal) ? o.Symbol[Pkg.Length..] : o.Symbol)
            .ToList();

    public static int LineOf(string text, string needle)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(needle, StringComparison.Ordinal))
            {
                return i;
            }
        }

        throw new InvalidOperationException("not found: " + needle);
    }
}
