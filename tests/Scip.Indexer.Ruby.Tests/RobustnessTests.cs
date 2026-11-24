namespace Scip.Indexer.Ruby.Tests;

public class RobustnessTests
{
    /// <summary>
    /// Smoke test on real-world Ruby (the installed stdlib, when a Ruby installation happens to exist; the indexer
    /// itself never needs one). Checks that nothing throws and that block structure stays balanced enough to
    /// produce sane enclosing ranges.
    /// </summary>
    [Test]
    public async Task IndexesInstalledStdlibWithoutErrors()
    {
        List<string> candidates = ["/usr/local/lib/ruby", "/usr/lib/ruby"];
        if (Directory.Exists("/opt/rbenv/versions"))
        {
            candidates.AddRange(Directory.EnumerateDirectories("/opt/rbenv/versions").Select(d => Path.Combine(d, "lib", "ruby")));
        }

        var roots = candidates.Where(Directory.Exists).ToList();
        if (roots.Count == 0)
        {
            Skip.Test("no Ruby installation to take sample sources from");
        }

        var files = roots.SelectMany(r => Directory.EnumerateFiles(r, "*.rb", SearchOption.AllDirectories)).Take(400).ToList();
        var sources = files.Select(f => (f, File.ReadAllText(f))).ToList();
        var docs = RubyIndexer.IndexSources(sources, new GemMetadata("stdlib", "x"));
        await Assert.That(docs.Count).IsEqualTo(files.Count);
        var occurrences = docs.Sum(d => d.Occurrences.Count);
        await Assert.That(occurrences).IsGreaterThan(1000);
        foreach (var doc in docs)
        {
            foreach (var o in doc.Occurrences)
            {
                await Assert.That(o.SingleLineRange.EndCharacter).IsGreaterThanOrEqualTo(o.SingleLineRange.StartCharacter);
            }
        }
    }

    [Test]
    public async Task CyclicMixinsSuperclassesAndAliasesTerminate()
    {
        const string src = """
            module A
              include B
              alias foo bar
              alias bar foo
              def x; foo; bar; baz; end
            end
            module B
              include A
              extend A
            end
            class C < D
              include A
              def go; x; foo; self.class.x; end
            end
            class D < C
              extend B
              def self.make; new.go; x; end
            end
            """;
        var docs = RubyIndexer.IndexSources([("cyc.rb", src)], new GemMetadata("cyc", "1"));
        await Assert.That(docs.Count).IsEqualTo(1);
        await Assert.That(docs[0].Occurrences.Count).IsGreaterThan(5);
    }
}
