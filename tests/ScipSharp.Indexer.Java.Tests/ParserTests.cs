using ScipSharp.Indexer.Java.Syntax;

namespace ScipSharp.Indexer.Java.Tests;

public class ParserTests
{
    internal static string? FindReference()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, ".reference", "scip-java");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        // worktrees live below <repo>/.claude/worktrees/<name>; .reference is in the main checkout
        var env = Environment.GetEnvironmentVariable("SCIP_REFERENCE_DIR");
        return env is not null ? Path.Combine(env, "scip-java") : null;
    }

    [Test]
    public async Task ParsesAllUpstreamJavaSources()
    {
        var root = FindReference();
        if (root is null) return;
        var failures = new List<string>();
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(root, "*.java", SearchOption.AllDirectories))
        {
            if (file.Contains("/expected/", StringComparison.Ordinal)) continue;
            if (Path.GetFileName(file) == "CompactMain.java") continue; // Java 25 compact source files are not supported
            count++;
            try { Parser.ParseFile(File.ReadAllText(file)); }
            catch (ParseException e) { failures.Add($"{file}@{e.Offset}: {e.Message}"); }
        }
        Console.WriteLine($"parsed {count} files, {failures.Count} failures");
        foreach (var f in failures.Take(30)) Console.WriteLine(f);
        await Assert.That(failures.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ParsesModernSyntax()
    {
        const string src = """
            package p;
            import java.util.*;
            import static java.lang.Math.max;
            public sealed interface Shape permits Circle, Sq {
              record Circle(double r) implements Shape { Circle { if (r < 0) throw new IllegalArgumentException(); } }
              record Sq(double s) implements Shape {}
              static double area(Shape s) {
                return switch (s) {
                  case Circle c when c.r() > 1 -> Math.PI * c.r() * c.r();
                  case Sq(double side) -> side * side;
                  default -> 0;
                };
              }
              default <T extends Comparable<? super T>> List<T> sort(List<T> in) {
                var out = new ArrayList<T>(in);
                out.sort((a, b) -> a.compareTo(b));
                Runnable r = () -> { int x = 1 >> 2; x >>>= 1; };
                String tb = \"\"\"
                  hi
                  \"\"\";
                for (var e : out) { if (e instanceof String str && str.isEmpty()) continue; }
                return out.stream().map(String::valueOf).toList() == null ? null : (List<T>) out;
              }
            }
            """;
        var cu = Parser.ParseFile(src.Replace("\\\"", "\"", StringComparison.Ordinal));
        await Assert.That(cu.Types.Single().Name).IsEqualTo("Shape");
        await Assert.That(cu.Types.Single().Members.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Parses_hex_floats_type_annotations_qualified_array_class_and_method_ref_type_args()
    {
        const string src = """
            class A {
              static final double D = -0x1p31 + 0x1.0p31 + 0x.8p-2f;
              java.util.@Nullable Optional<T> f(Map.@Nullable Entry<K, V> e) {
                Object a = Map.Entry[].class;
                Object b = java.util.Map.Entry[]::new;
                Object c = ImmutableMap.Builder<K, V>::new;
                Object d = Foo::<K, V>builder;
                return null;
              }
            }
            """;
        var cu = Parser.ParseFile(src);
        await Assert.That(cu.Types.Single().Members.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Merges_duplicate_relationships_across_documents_and_keeps_flags()
    {
        static Document Doc(string path, Relationship rel, params Relationship[] more)
        {
            var d = new Document { RelativePath = path };
            var info = new SymbolInformation { Symbol = "a#m()." };
            info.Relationships.Add(rel);
            info.Relationships.AddRange(more);
            d.Symbols.Add(info);
            return d;
        }
        var d1 = Doc("a/A.java", new Relationship { Symbol = "b#m().", IsImplementation = true });
        var d2 = Doc("b/A.java", new Relationship { Symbol = "b#m().", IsReference = true },
            new Relationship { Symbol = "b#m().", IsTypeDefinition = true }, new Relationship { Symbol = "c#m().", IsReference = true });
        var removed = RelationshipMerger.Merge([d1, d2]);
        await Assert.That(removed).IsEqualTo(2);
        var kept = d1.Symbols[0].Relationships.Single();
        await Assert.That(kept.IsImplementation && kept.IsReference && kept.IsTypeDefinition).IsTrue();
        await Assert.That(d2.Symbols[0].Relationships.Select(r => r.Symbol).Single()).IsEqualTo("c#m().");
    }
}
