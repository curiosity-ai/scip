namespace ScipSharp.Indexer.Python.Tests;

/// <summary>
/// The upstream scip-python snapshot suite (<c>packages/pyright-scip/snapshots</c>), run against the native port.
/// <para>
/// Files in <see cref="Divergent"/> are known to differ from the pyright-backed output (the reason is recorded). For
/// snapshots rendered with <c>showDocs</c>, documentation lines are not compared: upstream documentation comes from pyright's
/// hover provider (inferred types, typeshed docstrings), which this port does not have. Everything else is compared exactly.
/// </para>
/// </summary>
public class SnapshotTests
{
    /// <summary>"snapshot/relative/path" to the reason the output differs from upstream.</summary>
    private static readonly Dictionary<string, string> Divergent = new()
    {
        ["nameparts_import/nameparts.py"] =
            "upstream emits `local 0` for importlib.resources.read_text (typeshed re-export quirk); we resolve it to a stdlib symbol",
        ["request_goofiness/goofy.py"] =
            "needs a real installed `requests` distribution (site-packages sources) to resolve requests.get",
        ["issue_223/contracts.py"] =
            "needs isinstance() type narrowing (value.x) and keyword-argument resolution into stdlib stubs (dataclass(frozen=...))",
        ["issue_223/consumer.py"] = "needs isinstance()/inference through function return types for `Foo#x`",
        ["unique/walrus.py"] = "needs typeshed to know that re.match() returns re.Match (member Match#group())",
        ["unique/property_access.py"] = "needs generic element-type inference (Sequence[PropertyClass] iteration)",
    };

    public static IEnumerable<string> Names() => SnapshotHarness.AllSnapshots();

    [Test]
    [MethodDataSource(nameof(Names))]
    public async Task Snapshot(string name)
    {
        var rendered = SnapshotHarness.Render(name);
        await Assert.That(rendered.Count).IsGreaterThan(0);

        foreach (var (path, text) in rendered)
        {
            if (Divergent.ContainsKey($"{name}/{path}"))
            {
                continue;
            }

            var expected = SnapshotHarness.Expected(name, path);
            if (expected.Contains("format-options: showDocs", StringComparison.Ordinal))
            {
                await Assert.That(StripDocs(text)).IsEqualTo(StripDocs(expected));
            }
            else
            {
                await Assert.That(text).IsEqualTo(expected);
            }
        }
    }

    private static string StripDocs(string text) =>
        string.Join('\n', text.Split('\n').Where(l => !l.Contains("documentation ", StringComparison.Ordinal) && !l.TrimStart('#').TrimStart().StartsWith("> ", StringComparison.Ordinal) && l.TrimStart('#').Trim() != ">"));
}
