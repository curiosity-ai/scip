using Scip.Indexer.Php.Syntax;

namespace Scip.Indexer.Php.Tests;

/// <summary>Port of upstream <c>PosResolverTest::testPosInDoc</c> and the doc comment parsing it relies on.</summary>
public class DocCommentTests
{
    private static DocTags Parse(string text) => PhpDocParser.Parse(new DocComment(0, text.Length, text));

    private static string At(string text, int start, int end)
    {
        var l = new LineIndex(text);
        var (sl, sc) = l.At(start);
        var (el, ec) = l.At(end);
        return $"{sl},{sc},{el},{ec}";
    }

    [Test]
    [Arguments("/** @property int $foo */", "0,18,0,22")]
    [Arguments("/** @property int $foo additional documentation */", "0,18,0,22")]
    public async Task PropertyOnOneLine(string doc, string expected)
    {
        var p = Parse(doc).Properties.Single();
        await Assert.That(p.Name).IsEqualTo("$foo");
        await Assert.That(At(doc, p.NameStart, p.NameEnd)).IsEqualTo(expected);
    }

    [Test]
    public async Task PropertyWithoutTypeIsIgnored()
    {
        await Assert.That(Parse("/** @property $foo */").Properties).IsEmpty();
    }

    [Test]
    [Arguments("/** @method void foo() */", "0,17,0,20")]
    [Arguments("/** @method void foo() additional documentation */", "0,17,0,20")]
    public async Task MethodOnOneLine(string doc, string expected)
    {
        var m = Parse(doc).Methods.Single();
        await Assert.That(m.Name).IsEqualTo("foo");
        await Assert.That(At(doc, m.NameStart, m.NameEnd)).IsEqualTo(expected);
    }

    [Test]
    public async Task MultilineDocComment()
    {
        var doc = string.Join("\n",
            "/** @property int $p1",
            "  *  @property-read   ?Foo    $p2",
            "* @property-write Foo&Bar $p3 additional documentation",
            " * @method Foo m1() additionl documentation",
            " * @method Foo m2(int $p1, bool $p2, string $p3)",
            "* @property array<int, array{",
            "* id: int,",
            "* name: string,",
            "* }> $p3",
            "* @property int $p4 additional documentation line 1",
            "*                   additional documentation line 2",
            " */");
        var tags = Parse(doc);
        var props = tags.Properties.Select(p => $"{p.Name}@{At(doc, p.NameStart, p.NameEnd)}").ToArray();
        await Assert.That(props).IsEquivalentTo(["$p1@0,18,0,21", "$p2@1,30,1,33", "$p3@2,26,2,29", "$p3@8,5,8,8", "$p4@9,16,9,19"]);
        var methods = tags.Methods.Select(m => $"{m.Name}@{At(doc, m.NameStart, m.NameEnd)}").ToArray();
        await Assert.That(methods).IsEquivalentTo(["m1@3,15,3,17", "m2@4,15,4,17"]);
    }

    [Test]
    public async Task ParsesVarAndReturnTypes()
    {
        var tags = Parse("/**\n * @var ?Foo[]|\\Bar\\Baz $x\n * @return array{a: Foo, 1: Bar}\n */");
        await Assert.That(tags.VarType).IsTypeOf<DocUnion>();
        await Assert.That(tags.ReturnType).IsTypeOf<DocShape>();
        var shape = (DocShape)tags.ReturnType!;
        await Assert.That(shape.Items.Select(i => i.Key ?? "").ToArray()).IsEquivalentTo(["a", "1"]);
    }

    [Test]
    public async Task GenericsAreParsedButNotInterpreted()
    {
        var tags = Parse("/** @return array<int, Foo>|null */");
        await Assert.That(tags.ReturnType).IsTypeOf<DocUnion>();
        await Assert.That(((DocUnion)tags.ReturnType!).Types[0]).IsTypeOf<DocOther>();
    }
}
