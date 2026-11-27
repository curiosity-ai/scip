using System.Text;

namespace ScipSharp.Indexer.Pascal.Tests;

/// <summary>A throw-away project directory for in-memory indexer tests.</summary>
internal sealed class TestProject : IDisposable
{
    private readonly DirectoryInfo _dir = Directory.CreateTempSubdirectory("scip-pascal-");

    public string Root => _dir.FullName;

    public TestProject Add(string relativePath, string text) => Add(relativePath, Encoding.UTF8.GetBytes(text));

    public TestProject Add(string relativePath, byte[] bytes)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
        return this;
    }

    public Index Index(params string[] args) => PascalIndexer.BuildIndex(Root, args);

    public void Dispose() => _dir.Delete(recursive: true);

    /// <summary>Indexes a single file and returns its document.</summary>
    public static (Index Index, Document Document) Single(string fileName, string text, params string[] args)
    {
        using var p = new TestProject().Add(fileName, text);
        var index = p.Index(args);
        return (index, index.Documents.Single(d => d.RelativePath == fileName));
    }
}

internal static class DocumentExtensions
{
    /// <summary>Source text covered by an occurrence (single-line ranges, ASCII sources).</summary>
    public static string TextOf(this Occurrence occ, string source)
    {
        var lines = source.Split('\n');
        var r = occ.GetSourceRange()!.Value;
        return lines[r.Start.Line].Substring(r.Start.Character, r.End.Character - r.Start.Character);
    }

    /// <summary>Symbols of the occurrences whose text is <paramref name="name"/> (in source order).</summary>
    public static List<string> SymbolsAt(this Document doc, string source, string name, bool definitions = false) =>
        [.. doc.Occurrences
            .Where(o => (!definitions || (o.SymbolRoles & (int)SymbolRole.Definition) != 0) && o.TextOf(source) == name)
            .OrderBy(static o => o.Line()).ThenBy(static o => o.Column())
            .Select(static o => o.Symbol)];

    public static int Line(this Occurrence occ) => occ.GetSourceRange()!.Value.Start.Line;

    public static int Column(this Occurrence occ) => occ.GetSourceRange()!.Value.Start.Character;

    public static bool HasRole(this Occurrence occ, SymbolRole role) => (occ.SymbolRoles & (int)role) != 0;

    public static SymbolInformation? Info(this Index index, string symbol) =>
        index.Documents.SelectMany(static d => d.Symbols).Concat(index.ExternalSymbols).FirstOrDefault(s => s.Symbol == symbol);
}
