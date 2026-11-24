using Scip.Indexer.Java.Project;
using Scip.Indexer.Java.Syntax;

namespace Scip.Indexer.Java.Tests;

/// <summary>Behaviours that mirror the scip-java 0.12.3 (semanticdb-javac) output.</summary>
public class ParityTests
{
    private static async Task<Document> IndexAsync(string source, string file = "p/q/T.java")
    {
        var root = Directory.CreateTempSubdirectory("scipjava-parity").FullName;
        var path = Path.Combine(root, file);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, source);
        var project = new ProjectDiscovery(new ArtifactLocator([], [])).Discover(root);
        var output = Path.Combine(root, "index.scip");
        var rc = await NativeProjectIndexer.RunAsync(project, output, "17", [], new AggregatorReporter(), CancellationToken.None);
        await Assert.That(rc).IsEqualTo(0);
        return Index.Parser.ParseFrom(File.ReadAllBytes(output)).Documents.Single(d => d.RelativePath == file);
    }

    private static List<string> Items(params string[] items) => [.. items];

    private static HashSet<string> Symbols(Document doc) => doc.Occurrences.Select(o => o.Symbol.Replace("semanticdb maven . . ", "", StringComparison.Ordinal)).ToHashSet();

    [Test]
    public async Task NamedLocalClassesAreGlobalUnderTheirMethod()
    {
        var doc = await IndexAsync("""
            package p.q;
            class T {
              void test() {
                class Dummy { int i; }
                Dummy d = new Dummy();
                Object o = new Object() { int x; };
              }
            }
            """);
        var s = Symbols(doc);
        await Assert.That(s.Contains("p/q/T#test().Dummy#")).IsTrue();
        await Assert.That(s.Contains("p/q/T#test().Dummy#i.")).IsTrue();
        await Assert.That(s.Contains("p/q/T#test().Dummy#`<init>`().")).IsTrue();
        // anonymous classes and everything in them stay local
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.StartsWith("local ", StringComparison.Ordinal) && (o.SymbolRoles & 1) != 0)).IsTrue();
    }

    [Test]
    public async Task OverloadResolutionPicksTheMostSpecificMethodInTheFirstApplicablePhase()
    {
        var doc = await IndexAsync("""
            package p.q;
            class T {
              void f(Object o) {}
              void f(String s) {}
              void f(Integer i) {}
              void f(int i) {}
              void f(long l) {}
              void g() {
                f("x");
                f(1);
                f(Integer.valueOf(1));
                f(1L);
                f((Object) null);
              }
            }
            """);
        var refs = doc.Occurrences.Where(o => (o.SymbolRoles & 1) == 0 && o.Symbol.Contains("T#f(", StringComparison.Ordinal))
            .OrderBy(o => o.SingleLineRange.Line).Select(o => o.Symbol[(o.Symbol.IndexOf("T#f", StringComparison.Ordinal))..]).ToList();
        await Assert.That(refs).IsEquivalentTo(Items("T#f(+1).", "T#f(+3).", "T#f(+2).", "T#f(+4).", "T#f()."));
    }

    [Test]
    public async Task GenericMethodTypeVariableBoundsParticipateInApplicability()
    {
        var doc = await IndexAsync("""
            package p.q;
            class T {
              static <C extends Comparable<?>> void a(C c) {}
              static void a(Object o) {}
              enum E { X }
              void g() {
                a(E.X);
                a(new Object());
              }
            }
            """);
        var refs = doc.Occurrences.Where(o => (o.SymbolRoles & 1) == 0 && o.Symbol.Contains("T#a(", StringComparison.Ordinal) && o.Symbol.EndsWith(").", StringComparison.Ordinal))
            .OrderBy(o => o.SingleLineRange.Line).Select(o => o.Symbol[(o.Symbol.IndexOf("T#a", StringComparison.Ordinal))..]).ToList();
        await Assert.That(refs).IsEquivalentTo(Items("T#a().", "T#a(+1)."));
    }

    [Test]
    public async Task JavadocTextIsEmittedTheWayJavacDeliversIt()
    {
        var doc = await IndexAsync("""
            package p.q;
            /**
             * Multi line.
             *
             * @author me
             */
            class T {
              /** Single line. */
              int a;
              /**
               * Garbage text.
               */
              // an ordinary comment in between does not detach the doc comment
              int b;
            }
            """);
        string? Doc(string name) => doc.Symbols.FirstOrDefault(s => s.DisplayName == name)?.Documentation.SingleOrDefault();
        await Assert.That(Doc("T")).IsEqualTo(" Multi line.\n\n @author me\n");
        await Assert.That(Doc("a")).IsEqualTo("Single line. ");
        await Assert.That(Doc("b")).IsEqualTo(" Garbage text.\n");
    }

    [Test]
    [Arguments("/** a */", "a ")]
    [Arguments("/***/", "")]
    [Arguments("/**/", "")]
    [Arguments("/**\n * a\n * b\n */", " a\n b\n")]
    [Arguments("/** a\n   b **/", "a\n   b ")]
    [Arguments("/**\r\n * a\r\n */", " a\n")]
    public async Task ScansDocComments(string comment, string expected)
    {
        await Assert.That(Lexer.ScanDocComment(comment)).IsEqualTo(expected);
    }

    [Test]
    public async Task PackageDeclarationsAndQualifiedNamesReferenceEveryPackagePrefix()
    {
        var doc = await IndexAsync("""
            package p.q;
            class T {
              java.util.List<String> l;
              Object o = java.lang.Math.PI;
            }
            """);
        var s = Symbols(doc);
        await Assert.That(s.Contains("p/")).IsTrue();
        await Assert.That(s.Contains("p/q/")).IsTrue();
        await Assert.That(s.Contains("java/")).IsTrue();
        await Assert.That(s.Contains("java/util/")).IsTrue();
        await Assert.That(s.Contains("java/lang/")).IsTrue();
    }

    [Test]
    public async Task ThisSuperAndArrayMembersAreReferenced()
    {
        var doc = await IndexAsync("""
            package p.q;
            class T implements Cloneable {
              class In { T t() { return T.this; } }
              T copy() throws CloneNotSupportedException { return (T) super.clone(); }
              int[] c(int[] a) { int n = a.length; return a.clone(); }
              Class<?> k() { return int.class; }
            }
            """);
        var s = Symbols(doc);
        await Assert.That(s.Contains("p/q/T#this.")).IsTrue();
        await Assert.That(s.Contains("p/q/T#super.")).IsTrue();
        await Assert.That(s.Contains("Array#length.")).IsTrue();
        await Assert.That(s.Contains("Array#clone().")).IsTrue();
        await Assert.That(s.Contains("int#class.")).IsTrue();
    }

    [Test]
    public async Task ResourceVariablesGetGlobalTermSymbols()
    {
        var doc = await IndexAsync("""
            package p.q;
            class T {
              void m() throws Exception {
                try (java.io.StringReader r = new java.io.StringReader("")) { r.read(); }
              }
            }
            """);
        await Assert.That(Symbols(doc).Contains("p/q/T#m().r.")).IsTrue();
    }

    [Test]
    public async Task ConstructorReferencesSpanTheWholeExpression()
    {
        const string source = """
            package p.q;
            class T {
              java.util.function.Supplier<StringBuilder> s = StringBuilder::new;
            }
            """;
        var doc = await IndexAsync(source);
        var occ = doc.Occurrences.Single(o => o.Symbol.Contains("StringBuilder#`<init>`", StringComparison.Ordinal));
        await Assert.That(occ.SingleLineRange.EndCharacter - occ.SingleLineRange.StartCharacter).IsEqualTo("StringBuilder::new".Length);
    }

    [Test]
    public async Task StaticImportedMembersAreNotReferencedByTheImportItself()
    {
        var doc = await IndexAsync("""
            package p.q;
            import static java.lang.Math.max;
            class T { int m() { return max(1, 2); } }
            """);
        // the import line (line 1) names only the type and packages
        var onImport = doc.Occurrences.Where(o => o.SingleLineRange.Line == 1).Select(o => o.Symbol).ToList();
        await Assert.That(onImport.Any(s => s.Contains("Math#max", StringComparison.Ordinal))).IsFalse();
        await Assert.That(doc.Occurrences.Any(o => o.SingleLineRange.Line == 2 && o.Symbol.Contains("Math#max(", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    [Arguments("[17,)", 21, true)]
    [Arguments("[17,)", 11, false)]
    [Arguments("[1.8,11)", 8, true)]
    [Arguments("[1.8,11)", 11, false)]
    [Arguments("17", 17, true)]
    [Arguments("!17", 17, false)]
    public async Task EvaluatesJdkProfileActivation(string spec, int major, bool expected)
    {
        await Assert.That(Pom.JdkActivation(spec, major)).IsEqualTo(expected);
    }

    [Test]
    public async Task CompilerReleaseComesFromPropertiesPluginConfigurationAndProfiles()
    {
        var dir = Directory.CreateTempSubdirectory("scipjava-pom").FullName;
        var path = Path.Combine(dir, "pom.xml");
        await File.WriteAllTextAsync(path, """
            <project>
              <groupId>g</groupId><artifactId>a</artifactId><version>1</version>
              <properties><maven.compiler.release>8</maven.compiler.release></properties>
              <profiles>
                <profile>
                  <activation><jdk>[17,)</jdk></activation>
                  <properties><maven.compiler.testRelease>17</maven.compiler.testRelease></properties>
                </profile>
              </profiles>
            </project>
            """);
        var withProfile = Pom.Load(path, 21)!;
        await Assert.That(withProfile.Release(test: false)).IsEqualTo(8);
        await Assert.That(withProfile.Release(test: true)).IsEqualTo(17);
        var without = Pom.Load(path, 11)!;
        await Assert.That(without.Release(test: true)).IsEqualTo(8);
    }
}
