namespace Scip.Indexer.Dart.Tests;

public class DumpTests
{
    [Test]
    public async Task Dump()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.reference/scip-dart/snapshots/input/dart3-features"));
        var index = DartIndexer.Build(root, true);
        var expectedRoot = Path.GetFullPath(Path.Combine(root, "../../output/dart3-features"));
        foreach (var doc in index.Documents.Where(d => d.Language == "Dart"))
        {
            var exp = Path.Combine(expectedRoot, doc.RelativePath);
            if (!File.Exists(exp))
            {
                continue;
            }

            var expected = UpstreamSnapshot.Definitions(File.ReadAllText(exp));
            var actual = UpstreamSnapshot.Definitions(SnapshotRenderer.Render(doc, File.ReadAllText(Path.Combine(root, doc.RelativePath))));
            var missing = expected.Except(actual).ToList();
            var extra = actual.Except(expected).ToList();
            var target = Path.Combine("/tmp/claude-0/-home-user-scip/4219b77c-001b-5ab7-98b9-3a285a029a03/scratchpad/dump", doc.RelativePath + ".diff");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var er = UpstreamSnapshot.Definitions(File.ReadAllText(exp), "reference").Where(x => x.Contains("dart_test")).ToHashSet();
            var ar = UpstreamSnapshot.Definitions(SnapshotRenderer.Render(doc, File.ReadAllText(Path.Combine(root, doc.RelativePath))), "reference").Where(x => x.Contains("dart_test")).ToHashSet();
            await File.WriteAllLinesAsync(target, missing.Select(m => "MISSING " + m).Concat(extra.Select(m => "EXTRA " + m))
                .Concat(er.Except(ar).Select(m => "MISSINGREF " + m)).Concat(ar.Except(er).Select(m => "EXTRAREF " + m)));
        }
    }
}
