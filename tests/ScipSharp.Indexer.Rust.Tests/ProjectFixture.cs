using System.Text;
using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Rust.Tests;

/// <summary>
/// Multi-file/multi-crate fixtures in the spirit of rust-analyzer's <c>//- /path crate:name deps:foo</c> syntax: a
/// dictionary of relative paths to sources, one of which contains a <c>$0</c> cursor marker. The project is indexed
/// natively and the symbol of the occurrence under the cursor is returned.
/// </summary>
internal static class ProjectFixture
{
    public static async Task<(Index Index, string Root)> IndexAsync(Dictionary<string, string> files)
    {
        var dir = Directory.CreateTempSubdirectory("scip-rust-fx-").FullName;
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            await File.WriteAllTextAsync(full, content.Replace("$0", ""));
        }

        var output = Path.Combine(dir, "index.scip");
        var code = await new RustIndexer().IndexAsync(new IndexerOptions(dir, output, []), CancellationToken.None);
        if (code != 0)
        {
            throw new InvalidOperationException("indexing failed");
        }

        return (Index.Parser.ParseFrom(await File.ReadAllBytesAsync(output)), dir);
    }

    /// <summary>Symbol of the occurrence under the first <c>$0</c> (the character before it) in <paramref name="files"/>.</summary>
    public static async Task<string?> SymbolAtCursor(Dictionary<string, string> files)
    {
        var (index, _) = await IndexAsync(files);
        foreach (var (path, content) in files)
        {
            var at = content.IndexOf("$0", StringComparison.Ordinal);
            if (at < 0)
            {
                continue;
            }

            var clean = content.Replace("$0", "");
            var before = clean[..at];
            var line = before.Count(c => c == '\n');
            var col = Encoding.UTF8.GetByteCount(before[(before.LastIndexOf('\n') + 1)..]);
            var doc = index.Documents.Single(d => d.RelativePath == path);
            var occ = doc.Occurrences
                .Where(o => o.SingleLineRange is { } r && r.Line == line && r.StartCharacter <= col - 1 && col - 1 < r.EndCharacter)
                .OrderBy(o => o.SingleLineRange!.EndCharacter - o.SingleLineRange!.StartCharacter)
                .FirstOrDefault();
            if (occ is null)
            {
                return null;
            }

            if (occ.Symbol.StartsWith("local ", StringComparison.Ordinal))
            {
                var info = doc.Symbols.Single(s => s.Symbol == occ.Symbol);
                return "local enclosed by " + info.EnclosingSymbol;
            }

            return occ.Symbol;
        }

        return null;
    }

    public static Dictionary<string, string> Workspace(params (string Path, string Content)[] files)
    {
        var d = files.ToDictionary(f => f.Path, f => f.Content);
        return d;
    }

    public static string Manifest(string name, string? version = null, string deps = "") =>
        $"[package]\nname = \"{name}\"\n{(version is null ? "" : $"version = \"{version}\"\n")}\n[dependencies]\n{deps}\n";
}
