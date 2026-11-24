using System.Text;
using Scip.Indexer.Php.Syntax;

namespace Scip.Indexer.Php.Tests;

public class ParserTests
{
    private static string TestData => Path.Combine(AppContext.BaseDirectory, "testdata");

    private static ParsedFile ParseText(string php) => PhpParser.Parse(php);

    public static IEnumerable<string> AllFixtureFiles() =>
        Directory.EnumerateFiles(TestData, "*.php", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(TestData, f))
            .Order(StringComparer.Ordinal);

    [Test]
    [MethodDataSource(nameof(AllFixtureFiles))]
    public async Task ParsesEveryFixtureFile(string relative)
    {
        var path = Path.Combine(TestData, relative);
        var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
        if (text.Length == 0)
        {
            return;
        }

        await Assert.That(() => PhpParser.Parse(text)).ThrowsNothing();
    }

    [Test]
    public async Task EmptyFileCannotBeIndexed()
    {
        var warnings = new List<string>();
        var parsed = new Scip.Indexer.Php.Composer.PhpFs().Parse(Path.Combine(TestData, "parser", "empty.php"), warnings.Add);
        await Assert.That(parsed).IsNull();
        await Assert.That(warnings.Count).IsEqualTo(1);
    }

    // Port of upstream PosResolverTest::testPos.
    [Test]
    public async Task PositionsAreZeroBasedByteOffsets()
    {
        var text = Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(TestData, "parser", "test.php")));
        var parsed = ParseText(text);
        var lines = new LineIndex(text);
        var seen = new List<string>();
        foreach (var n in parsed.PostOrder())
        {
            switch (n)
            {
                case Variable { Name: "x" }:
                    seen.Add(Fmt(lines, n.Start, n.End));
                    await Assert.That(Fmt(lines, n.Start, n.End)).IsEqualTo("2,0,2,2");
                    break;
                case Variable { Name: "yy" }:
                    await Assert.That(Fmt(lines, n.Start, n.End)).IsEqualTo("3,0,3,3");
                    break;
                case NewExpr:
                    await Assert.That(Fmt(lines, n.Start, n.End)).IsEqualTo("4,7,6,1");
                    break;
                case PropertyItem { Name.Name: "p" } p:
                    await Assert.That(Fmt(lines, p.Name.Start, p.Name.End)).IsEqualTo("5,18,5,20");
                    break;
                case PropertyFetch { NameNode: Ident { Name: "p" } id }:
                    await Assert.That(Fmt(lines, id.Start, id.End)).IsEqualTo("8,11,8,12");
                    break;
            }
        }

        await Assert.That(seen.Count).IsEqualTo(1);
    }

    private static string Fmt(LineIndex l, int s, int e)
    {
        var (sl, sc) = l.At(s);
        var (el, ec) = l.At(e);
        return $"{sl},{sc},{el},{ec}";
    }

    [Test]
    public async Task ColumnsAreUtf8ByteOffsets()
    {
        // "é" is two bytes in UTF-8; the source is decoded as Latin-1 so each byte counts once.
        var bytes = Encoding.UTF8.GetBytes("<?php\n$é = 1; $x = 2;\n");
        var parsed = ParseText(Encoding.Latin1.GetString(bytes));
        var lines = new LineIndex(Encoding.Latin1.GetString(bytes));
        var x = parsed.PostOrder().OfType<Variable>().Single(v => v.Name == "x");
        await Assert.That(lines.At(x.Start)).IsEqualTo((1, 9));
    }

    [Test]
    public async Task ResolvesNamesLikePhpParserNameResolver()
    {
        var parsed = ParseText("""
            <?php
            namespace App\Models;

            use Vendor\Lib\Thing;
            use Vendor\Lib\Other as O;
            use function Vendor\Lib\helper;
            use const Vendor\Lib\LIMIT;
            use Vendor\Group\{A, B as BB};

            new Thing();
            new O\Sub();
            new Local();
            new \Abs\Thing();
            new namespace\Rel();
            helper();
            plain();
            echo LIMIT, OTHER;
            class X extends Thing implements O { function f(self $a, ?Local $b): static {} }
            """);
        var names = parsed.PostOrder().OfType<Name>().Where(n => n.Parent is not UseItem and not UseStmt and not NamespaceStmt).Select(n => n.Text).ToList();
        await Assert.That(string.Join(",", names)).IsEqualTo(string.Join(",", 
        [
            "Vendor\\Lib\\Thing", "Vendor\\Lib\\Other\\Sub", "App\\Models\\Local", "Abs\\Thing", "App\\Models\\Rel", "Vendor\\Lib\\helper", "plain",
            "Vendor\\Lib\\LIMIT", "OTHER", "Vendor\\Lib\\Thing", "Vendor\\Lib\\Other", "self", "App\\Models\\Local", "static",
        ]));
    }

    [Test]
    public async Task ParsesModernSyntax()
    {
        var parsed = ParseText("""
            <?php
            declare(strict_types=1);
            namespace N {
                #[Attr(1), Other]
                enum Suit: string implements I {
                    case H = 'h';
                    const X = self::H;
                    public function label(): string { return match($this) { self::H => 'x', default => 'y' }; }
                }
                abstract class A {
                    public function __construct(private readonly int|string $a = 1, protected ?B $b = null, public array $c = [1, 'a' => 2]) {}
                    abstract protected static function f(int ...$xs): ?static;
                    use T { T::x insteadof U; U::x as protected y; }
                    public function g() {
                        $f = fn(int $x): int => $x * 2;
                        $g = static function () use (&$f): void { yield from [1]; };
                        $o = new class(1) extends A { };
                        echo "a {$o->b} $o->c[0] ${f}", <<<EOT
                          Heredoc {$o->g()} $f
                        EOT;
                        foreach ($this->xs as $k => [$a, $b]): echo $a; endforeach;
                        try { throw new \Exception('x'); } catch (A | B $e) { } finally { }
                        $x?->y?->z(...$args, named: 1);
                        $s = strlen(...);
                        return $a ?: $b ?? $c;
                    }
                }
                interface I extends J, K { const A = 1; public function m(): void; }
            }
            """);
        await Assert.That(parsed.Stmts.Count).IsEqualTo(2);
        var ns = (NamespaceStmt)parsed.Stmts[1];
        await Assert.That(ns.Stmts.OfType<ClassLike>().Count()).IsEqualTo(3);
    }

    [Test]
    public async Task AnonymousNamesUsePhpTokenIndices()
    {
        // `<?php\n` is token 0, `$x` 1, ` ` 2, `=` 3, ` ` 4, `new` 5, ` ` 6, `class` 7.
        var parsed = ParseText("<?php\n$x = new class {};\n");
        var cls = parsed.PostOrder().OfType<ClassStmt>().Single();
        await Assert.That(cls.TokPos).IsEqualTo(7);
        var parsed2 = ParseText("<?php\n$f = function () {};\n");
        await Assert.That(parsed2.PostOrder().OfType<Closure>().Single().TokPos).IsEqualTo(5);
    }

    [Test]
    public async Task ReportsSyntaxErrors()
    {
        await Assert.That(() => ParseText("<?php class { ")).Throws<ParseException>();
    }
}
