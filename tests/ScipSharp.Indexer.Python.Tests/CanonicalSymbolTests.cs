using ScipSharp.Indexer.Python.Emit;

namespace ScipSharp.Indexer.Python.Tests;

/// <summary>
/// Every symbol the indexer emits (occurrences, symbol information, relationships, external symbols) must parse with
/// <see cref="SymbolParser"/> and re-format to exactly the same string; locals must be <c>local </c> + simple identifier.
/// </summary>
public class CanonicalSymbolTests
{
    public static IEnumerable<string> Names() => SnapshotHarness.AllSnapshots();

    private const string Fixture = """
        from typing import Any
        import os

        data = {1: 2}
        pairs = [(y := v) for v in data if (settings := v)]
        total = sum(on_phase for on_phase in range(3) if (component_type := on_phase))

        def outer(a, b=1, *args, **kwargs):
            value = 1
            point = lambda x, y: (x, y)
            def inner(y, z=value):
                state = y
                state.attr = z
                return [(vp := q) for q in range(y)]
            class Local:
                group = 1
                def method(self, n):
                    self.field = n
                    return n
            return inner, Local, point

        class Cls:
            attr: int = 1
            def __init__(self, ünïcode, 名前):
                self.ünïcode = ünïcode
                self.名前 = 名前
                lam = lambda é: é
                for i, (j, k) in enumerate([(1, 2)]):
                    self.x = i + j + k
                with open(os.devnull) as fh:
                    pass
                try:
                    pass
                except Exception as exc:
                    raise exc

        def f(x: "Cls") -> Any:
            def g(): return x
            return g
        """;

    [Test]
    [MethodDataSource(nameof(Names))]
    public async Task SnapshotSymbolsAreCanonical(string name)
    {
        var bad = Check(Path.Combine(SnapshotHarness.SnapshotRoot, "input", name));
        await Assert.That(bad).IsEmpty();
    }

    [Test]
    public async Task GeneratedFixtureSymbolsAreCanonical()
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-python-canon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "fixture.py"), Fixture);
            var bad = Check(dir);
            await Assert.That(bad).IsEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static List<string> Check(string projectRoot)
    {
        var output = Path.Combine(Path.GetTempPath(), "scip-python-canon-" + Guid.NewGuid().ToString("N") + ".scip");
        try
        {
            var code = PythonIndexer.Run(projectRoot, output, new PythonIndexOptions
            {
                ProjectName = "proj",
                ProjectVersion = "1.0",
                EnvironmentFile = Path.Combine(SnapshotHarness.SnapshotRoot, "testEnv.json"),
                PythonVersion = "3.11",
                InferVersionFromCommit = false,
                Quiet = true,
            });
            if (code != 0)
            {
                throw new InvalidOperationException("exit " + code);
            }

            var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
            var bad = new List<string>();

            void Verify(string symbol)
            {
                if (symbol.Length == 0)
                {
                    return;
                }

                if (SymbolNames.IsLocal(symbol))
                {
                    var id = symbol["local ".Length..];
                    if (id.Length == 0 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '+' or '-' or '$'))
                    {
                        bad.Add(symbol);
                    }
                }
                else if (!SymbolNames.IsCanonical(symbol))
                {
                    bad.Add(symbol);
                }
            }

            foreach (var doc in index.Documents)
            {
                foreach (var o in doc.Occurrences)
                {
                    Verify(o.Symbol);
                }

                foreach (var s in doc.Symbols)
                {
                    Verify(s.Symbol);
                    foreach (var r in s.Relationships)
                    {
                        Verify(r.Symbol);
                    }
                }
            }

            foreach (var s in index.ExternalSymbols)
            {
                Verify(s.Symbol);
                foreach (var r in s.Relationships)
                {
                    Verify(r.Symbol);
                }
            }

            return bad;
        }
        finally
        {
            File.Delete(output);
        }
    }
}
