using System.Runtime.CompilerServices;
using ScipSharp.Cli.Linting;

namespace ScipSharp.Indexer.Pli.Tests;

internal static class TestHelpers
{
    /// <summary>The checked-in testdata directory (source tree, so snapshot updates land in git).</summary>
    public static string SourceTestData([CallerFilePath] string path = "") => Path.Combine(Path.GetDirectoryName(path)!, "testdata");

    /// <summary>Indexes a throw-away project made of <paramref name="files"/> (path → text).</summary>
    public static Index IndexFiles(IReadOnlyDictionary<string, string> files, params string[] args)
    {
        var dir = Directory.CreateTempSubdirectory("scip-pli-");
        try
        {
            foreach (var (path, text) in files)
            {
                var full = Path.Combine(dir.FullName, path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                File.WriteAllText(full, text);
            }

            return Index(dir.FullName, args);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>Indexes one free-form file <c>test.pli</c>.</summary>
    public static Index IndexSource(string text, params string[] args) =>
        IndexFiles(new Dictionary<string, string> { ["test.pli"] = text }, ["--margins=none", .. args]);

    public static Index Index(string root, params string[] args) =>
        new PliProjectIndexer(root, PliOptions.Parse(args), args).Build();

    /// <summary>Lint diagnostics (messages) of an index.</summary>
    public static List<string> Lint(Index index) => IndexLinter.Lint(index).Select(static d => d.Message).ToList();

    /// <summary>The source text covered by an occurrence (single-line ranges).</summary>
    public static string TextOf(string source, Occurrence o)
    {
        var r = o.GetSourceRange()!.Value;
        var line = source.Split('\n')[r.Start.Line];
        return line[r.Start.Character..r.End.Character];
    }

    /// <summary>(text, symbol, isDefinition) of every occurrence in document order.</summary>
    public static List<(string Text, string Symbol, bool Definition)> Occurrences(Index index, string source, string path = "test.pli")
    {
        var doc = index.Documents.Single(d => d.RelativePath == path);
        return doc.Occurrences
            .Select(o => (TextOf(source, o), o.Symbol, (o.SymbolRoles & (int)SymbolRole.Definition) != 0))
            .ToList();
    }

    /// <summary>The symbols referenced (or defined) at every occurrence of <paramref name="name"/>.</summary>
    public static List<string> SymbolsAt(Index index, string source, string name, string path = "test.pli") =>
        Occurrences(index, source, path).Where(o => o.Text.Equals(name, StringComparison.OrdinalIgnoreCase)).Select(o => o.Symbol).ToList();
}
