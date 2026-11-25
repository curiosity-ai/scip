using Google.Protobuf;

namespace ScipSharp.Cli.Tests;

internal static class TestHelpers
{
    private static readonly Regex SymbolRegex = new("([0-9a-zA-Z]*)(.*)");
    private static readonly Regex RelationsRegex = new("~([0-9a-zA-Z]*)#([rdit]*)");

    /// <summary>
    /// Compactly specifies symbol information: <c>name ('~' name '#' kinds)*</c> where kinds are r, d, i, t.
    /// </summary>
    public static SymbolInformation ParseSymbolInfo(string text)
    {
        var m = SymbolRegex.Match(text);
        var info = new SymbolInformation { Symbol = m.Groups[1].Value };
        foreach (Match rel in RelationsRegex.Matches(m.Groups[2].Value))
        {
            var flags = rel.Groups[2].Value;
            info.Relationships.Add(new Relationship
            {
                Symbol = rel.Groups[1].Value,
                IsReference = flags.Contains('r'),
                IsDefinition = flags.Contains('d'),
                IsImplementation = flags.Contains('i'),
                IsTypeDefinition = flags.Contains('t'),
            });
        }

        return info;
    }

    public static Occurrence PlaceholderOccurrence(string symbol) =>
        new() { Symbol = symbol, Range = { 0, 0, 0 }, SymbolRoles = 0 };

    public static Index MakeIndex(
        string[] extSyms,
        Dictionary<string, string[]>? docSyms,
        Dictionary<string, string[]>? docOccs)
    {
        var index = new Index();
        index.ExternalSymbols.AddRange(extSyms.Select(ParseSymbolInfo));
        docOccs = docOccs is null ? [] : new Dictionary<string, string[]>(docOccs);
        foreach (var (path, symbols) in docSyms ?? [])
        {
            var doc = new Document { RelativePath = path };
            doc.Symbols.AddRange(symbols.Select(ParseSymbolInfo));
            if (docOccs.Remove(path, out var occs))
            {
                doc.Occurrences.AddRange(occs.Select(PlaceholderOccurrence));
            }

            index.Documents.Add(doc);
        }

        foreach (var (path, occs) in docOccs)
        {
            var doc = new Document { RelativePath = path };
            doc.Occurrences.AddRange(occs.Select(PlaceholderOccurrence));
            index.Documents.Add(doc);
        }

        return index;
    }

    /// <summary>Runs the CLI in-process, capturing stdout/stderr.</summary>
    public static async Task<(int ExitCode, string Output, string Error)> RunCli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = await CliApp.RunAsync(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    public static string WriteIndex(DirectoryInfo dir, Index index, string name = "index.scip")
    {
        var path = Path.Combine(dir.FullName, name);
        File.WriteAllBytes(path, index.ToByteArray());
        return path;
    }

    public static Occurrence Occ(string symbol, Range range, SymbolRole? role = null)
    {
        var o = new Occurrence { Symbol = symbol, SymbolRoles = role is { } r ? (int)r : 0 };
        o.SetSourceRange(range);
        return o;
    }
}
