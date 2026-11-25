using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Dart.Tests;

/// <summary>Local scopes, doc comment references, member chains and SDK/dependency symbols.</summary>
public class ResolutionTests
{
    private static Document Doc(DartIndex index, string path) => index.Documents.Single(d => d.RelativePath == path);

    private static List<(string Text, string Symbol, bool Definition)> Occurrences(Document doc, string source)
    {
        var lines = source.Split('\n');
        var result = new List<(string, string, bool)>();
        foreach (var o in doc.Occurrences)
        {
            if (o.SingleLineRange is not { } r)
            {
                continue;
            }

            result.Add((lines[r.Line][r.StartCharacter..r.EndCharacter], o.Symbol, (o.SymbolRoles & (int)SymbolRole.Definition) != 0));
        }

        return result;
    }

    private static (Document Doc, List<(string Text, string Symbol, bool Definition)> Occs) Index(string source)
    {
        var root = Fixtures.TempPackage("demo", new() { ["lib/a.dart"] = source });
        var doc = Doc(DartIndexer.Build(root), "lib/a.dart");
        return (doc, Occurrences(doc, source));
    }

    [Test]
    public async Task LocalVariablesAndClosureParametersAreDefinedAndReferenced()
    {
        const string source = """
            int run(List<int> items) {
              final total = items.length;
              var sum = 0;
              items.forEach((item) {
                sum += item;
              });
              try {
                return total + sum;
              } catch (error, stack) {
                print(error);
                return 0;
              }
            }
            """;
        var (_, occs) = Index(source);

        string Local(string text, bool definition) => occs.First(o => o.Text == text && o.Definition == definition).Symbol;

        await Assert.That(Local("total", true)).StartsWith("local ");
        await Assert.That(occs.Where(o => o.Text == "total" && !o.Definition).Select(o => o.Symbol)).Contains(Local("total", true));
        await Assert.That(occs.Where(o => o.Text == "sum" && !o.Definition).Select(o => o.Symbol).Distinct()).IsEquivalentTo([Local("sum", true)]);
        await Assert.That(occs.Where(o => o.Text == "item" && !o.Definition).Select(o => o.Symbol)).Contains(Local("item", true));

        // catch parameters are only ever referenced upstream, never defined
        await Assert.That(occs.Any(o => o.Text == "error" && o.Definition)).IsFalse();
        await Assert.That(occs.Where(o => o.Text == "error").Select(o => o.Symbol).Single()).StartsWith("local ");
    }

    [Test]
    public async Task DocCommentReferencesResolveToParametersAndMembers()
    {
        const string source = """
            class Box {
              final int size;

              /// Grows by [amount] but never past [size], see [Box.shrink].
              void grow(int amount) {}

              void shrink() {}
            }
            """;
        var (_, occs) = Index(source);

        await Assert.That(occs.Any(o => o.Text == "amount" && !o.Definition && o.Symbol.StartsWith("local ", StringComparison.Ordinal))).IsTrue();
        await Assert.That(occs.Any(o => o.Text == "size" && !o.Definition && o.Symbol.EndsWith("Box#size.", StringComparison.Ordinal))).IsTrue();
        await Assert.That(occs.Any(o => o.Text == "shrink" && !o.Definition && o.Symbol.EndsWith("Box#shrink().", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task MemberChainsCascadesAndNamedArgumentsUseDeclaredTypes()
    {
        const string source = """
            class Engine {
              int power = 0;
              Engine boost({int by = 1}) => this;
            }

            class Car {
              final Engine engine = Engine();
              Engine get motor => engine;
            }

            void main() {
              final car = Car();
              car.motor.boost(by: 2).power;
              car.engine..boost()..power;
            }
            """;
        var (_, occs) = Index(source);

        bool Has(string text, string suffix) => occs.Any(o => o.Text == text && !o.Definition && o.Symbol.EndsWith(suffix, StringComparison.Ordinal));

        await Assert.That(Has("motor", "Car#`<get>motor`.")).IsTrue();
        await Assert.That(Has("boost", "Engine#boost().")).IsTrue();
        await Assert.That(Has("by", "Engine#boost().(by)")).IsTrue();
        await Assert.That(Has("power", "Engine#power.")).IsTrue();
        await Assert.That(Has("engine", "Car#engine.")).IsTrue();
        await Assert.That(occs.Count(o => o.Text == "boost" && !o.Definition)).IsEqualTo(2);
    }

    [Test]
    public async Task SdkSymbolsComeFromTheEmbeddedTable()
    {
        const string source = """
            class Stamp {
              @override
              String toString() => Uri.parse('x').toString();

              Duration get zero => Duration.zero;
            }
            """;
        var (doc, occs) = Index(source);

        await Assert.That(occs.Any(o => o.Text == "String" && o.Symbol.Contains("dart:core") && o.Symbol.EndsWith("String#", StringComparison.Ordinal))).IsTrue();
        await Assert.That(occs.Any(o => o.Text == "override" && o.Symbol.EndsWith("override.", StringComparison.Ordinal))).IsTrue();
        await Assert.That(occs.Any(o => o.Text == "parse" && o.Symbol.EndsWith("Uri#parse().", StringComparison.Ordinal))).IsTrue();
        await Assert.That(occs.Any(o => o.Text == "zero" && o.Symbol.EndsWith("Duration#zero.", StringComparison.Ordinal))).IsTrue();

        // `toString` overrides Object#toString: reported as an implementation relationship
        var toString = doc.Symbols.Single(s => s.Symbol.EndsWith("Stamp#toString().", StringComparison.Ordinal));
        await Assert.That(toString.Relationships.Any(r => r.Symbol.EndsWith("Object#toString().", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task SameNamedClassesResolveThroughImports()
    {
        var root = Fixtures.TempPackage("demo", new()
        {
            ["lib/a.dart"] = "class Thing {}\n",
            ["lib/b.dart"] = "class Thing {}\n",
            ["lib/use.dart"] = "import 'b.dart';\nThing make() => Thing();\n",
        });

        var doc = Doc(DartIndexer.Build(root), "lib/use.dart");
        var refs = doc.Occurrences.Where(o => (o.SymbolRoles & (int)SymbolRole.Definition) == 0 && o.Symbol.Contains("Thing#")).ToList();
        await Assert.That(refs).IsNotEmpty();
        await Assert.That(refs.All(o => o.Symbol.Contains("`b.dart`"))).IsTrue();
    }
}
