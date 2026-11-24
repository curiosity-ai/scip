using System.Text.RegularExpressions;
using Scip.Indexer.Clang;
using Scip.Indexing;

namespace Scip.Indexer.Clang.Tests;

/// <summary>
/// Runs the (pure C#) indexer on upstream <c>test/index</c> inputs and measures how many of the definition/reference
/// expectations of the upstream snapshot it reproduces. Function-overload hashes differ from upstream (different
/// hash of a different canonical type string), so they are normalised away; relationships, documentation and
/// <c>&lt;file&gt;</c> symbols are not compared. The per-file minimums below are regression floors, not goals.
/// </summary>
public partial class SnapshotTests
{
    private sealed record Expect(int Line, int Col, int Len, bool Definition, string Symbol);

    [GeneratedRegex(@"^//( *)(\^+) (definition|reference) \[\.\.\] (.*)$")]
    private static partial Regex ExpectLine();

    [GeneratedRegex(@"\([0-9a-f]{16}\)\.")]
    private static partial Regex Hash();

    [GeneratedRegex(@"\$anonymous_type_[0-9a-f]+_")]
    private static partial Regex AnonType();

    private static string Normalize(string symbol, bool exact = false)
    {
        var s = symbol.StartsWith("cxx . . $ ", StringComparison.Ordinal) ? symbol["cxx . . $ ".Length..] : symbol;
        return AnonType().Replace(exact ? s : Hash().Replace(s, "()."), "\\$anonymous_type_");
    }

    private static List<Expect> ParseSnapshot(string path)
    {
        var result = new List<Expect>();
        var line = -1;
        foreach (var l in File.ReadLines(path))
        {
            var m = ExpectLine().Match(l);
            if (m.Success)
            {
                result.Add(new Expect(line, m.Groups[1].Length, m.Groups[2].Length, m.Groups[3].Value == "definition", m.Groups[4].Value));
            }
            else if (!l.StartsWith("//", StringComparison.Ordinal))
            {
                line++;
            }
        }

        return result;
    }

    private static async Task<Scip.Index> RunAsync(string dir, string name)
    {
        var work = Directory.CreateTempSubdirectory("scip-clang-test").FullName;
        var data = Path.Combine(AppContext.BaseDirectory, "Data");
        var prefix = $"index_{dir}_";
        foreach (var f in Directory.EnumerateFiles(data, prefix + "*").Where(static f => !f.Contains(".snapshot.", StringComparison.Ordinal)))
        {
            File.Copy(f, Path.Combine(work, Path.GetFileName(f)[prefix.Length..]));
        }

        var src = Path.Combine(work, name + ".cc");
        var std = File.ReadLines(src).FirstOrDefault(static l => l.StartsWith("// extra-args:", StringComparison.Ordinal))?["// extra-args:".Length..].Trim() ?? "-std=c++17";
        File.WriteAllText(Path.Combine(work, "compile_commands.json"),
            System.Text.Json.JsonSerializer.Serialize(new[] { new { directory = work, file = src, arguments = new[] { "cc", std, "-c", src } } }));
        var output = Path.Combine(work, "index.scip");
        var code = await new ClangIndexer().IndexAsync(new IndexerOptions(work, output, []), CancellationToken.None);
        await Assert.That(code).IsEqualTo(0);
        return Scip.Index.Parser.ParseFrom(File.ReadAllBytes(output));
    }

    private static async Task<(double Coverage, List<string> Missing)> MeasureAsync(string dir, string name, string document, bool exact = false)
    {
        var index = await RunAsync(dir, name);
        var doc = index.Documents.Single(d => d.RelativePath == document);
        var actual = doc.Occurrences
            .Select(o => (o.SingleLineRange.Line, o.SingleLineRange.StartCharacter, Len: o.SingleLineRange.EndCharacter - o.SingleLineRange.StartCharacter,
                Def: (o.SymbolRoles & 1) != 0, Sym: Normalize(o.Symbol, exact)))
            .ToHashSet();
        var snapshot = Path.Combine(AppContext.BaseDirectory, "Data", $"index_{dir}_{name}.snapshot.cc");
        var missing = new List<string>();
        var total = 0;
        foreach (var e in ParseSnapshot(snapshot))
        {
            if (e.Symbol.Contains("<file>", StringComparison.Ordinal))
            {
                continue;
            }

            total++;
            var isLocal = e.Symbol.StartsWith("local ", StringComparison.Ordinal);
            var ok = isLocal
                ? actual.Any(a => a.Line == e.Line && a.StartCharacter == e.Col && a.Def == e.Definition && a.Sym.StartsWith("local ", StringComparison.Ordinal))
                : actual.Contains((e.Line, e.Col, e.Len, e.Definition, Normalize(e.Symbol, exact)));
            if (!ok)
            {
                var got = string.Join("|", actual.Where(a => a.Line == e.Line && a.StartCharacter == e.Col).Select(a => a.Sym));
                missing.Add($"{e.Line}:{e.Col} {(e.Definition ? "def" : "ref")} {e.Symbol}  GOT[{got}]");
            }
        }

        return (total == 0 ? 1 : 1.0 - ((double)missing.Count / total), missing);
    }

    [Test]
    [Arguments("functions", "functions", 1.0)]
    [Arguments("functions", "methods", 0.95)]
    [Arguments("vars", "vars", 1.0)]
    [Arguments("functions", "ctors_dtors", 0.8)]
    [Arguments("functions", "operators", 0.5)]
    [Arguments("functions", "body", 0.1)]
    [Arguments("functions", "templates", 1.0)]
    [Arguments("types", "templates", 0.7)]
    [Arguments("types", "types", 0.8)]
    [Arguments("types", "inheritance", 0.9)]
    [Arguments("vars", "members", 0.8)]
    [Arguments("vars", "templates", 0.85)]
    [Arguments("namespaces", "namespaces", 0.8)]
    public async Task UpstreamSnapshotCoverage(string dir, string name, double minimum)
    {
        var (coverage, missing) = await MeasureAsync(dir, name, name + ".cc");
        Console.WriteLine($"COVERAGE {dir}/{name}: {coverage:P0}; missing: {string.Join("; ", missing.Take(40))}");
        await Assert.That(coverage).IsGreaterThanOrEqualTo(minimum);
    }

    [Test]
    [Arguments("functions", "functions", 1.0)]
    [Arguments("functions", "methods", 0.9)]
    [Arguments("functions", "ctors_dtors", 0.8)]
    [Arguments("functions", "operators", 0.5)]
    [Arguments("functions", "templates", 0.9)]
    [Arguments("types", "templates", 0.7)]
    [Arguments("types", "types", 0.8)]
    [Arguments("types", "inheritance", 0.9)]
    [Arguments("vars", "members", 0.8)]
    [Arguments("vars", "templates", 0.85)]
    public async Task UpstreamSnapshotCoverageExactHashes(string dir, string name, double minimum)
    {
        var (coverage, missing) = await MeasureAsync(dir, name, name + ".cc", exact: true);
        Console.WriteLine($"EXACT {dir}/{name}: {coverage:P0}; missing: {string.Join("; ", missing.Take(40))}");
        await Assert.That(coverage).IsGreaterThanOrEqualTo(minimum);
    }
}
