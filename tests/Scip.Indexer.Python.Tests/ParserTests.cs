using Scip.Indexer.Python.Syntax;

namespace Scip.Indexer.Python.Tests;

public class ParserTests
{
    private static ModuleNode Parse(string text) => Parser.Parse(text);

    [Test]
    public async Task ParsesStatementsAndKeepsPositions()
    {
        var m = Parse("import os\nx = 1\n\ndef f(a, b=2, *args, **kw):\n    return a + b\n");
        await Assert.That(m.SyntaxErrors).IsEqualTo(0);
        await Assert.That(m.Statements.Count).IsEqualTo(3);
        var f = (FunctionNode)m.Statements[2];
        await Assert.That(f.Name.Value).IsEqualTo("f");
        await Assert.That(f.Parameters.Count).IsEqualTo(4);
        await Assert.That(f.Start).IsEqualTo("import os\nx = 1\n\n".Length);
    }

    [Test]
    public async Task DecoratedFunctionRangeIncludesDecorators()
    {
        var text = "@dec\ndef f():\n    pass\n";
        var f = (FunctionNode)Parse(text).Statements[0];
        await Assert.That(f.Start).IsEqualTo(0);
        await Assert.That(f.End).IsEqualTo(text.TrimEnd().Length);
    }

    [Test]
    public async Task ParsesModernSyntax()
    {
        var text = """
            from __future__ import annotations
            import a.b.c as d
            async def g(x: int | None = None, /, *, y: "str" = "s") -> list[int]:
                async with open_it() as (p, q), other() as r:
                    async for i in aiter(p):
                        yield i
                if (n := len(q)) > 1 and not r:
                    await h(*q, **{"k": n})
                return [j for j in range(n) if j % 2 if j > 3]

            class C(Base, metaclass=Meta):
                x: int = 3
                def m(self): return lambda a, *b, c=1: (a, b, c)

            match command.split():
                case [action]:
                    pass
                case Point(x=0, y=yy) | other if yy > 1:
                    pass
            try:
                pass
            except* (ValueError, TypeError) as e:
                raise RuntimeError("x") from e
            finally:
                del e
            print(f"{x!r:>{width}} {'nested'} {y=} {{literal}}")
            """;
        var m = Parse(text);
        await Assert.That(string.Join("; ", m.ErrorList.Select(e => text.Substring(e.Offset, Math.Min(40, text.Length - e.Offset)) + " => " + e.Message))).IsEqualTo("");
        await Assert.That(m.Statements.OfType<MatchNode>().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task FStringFieldsAreParsedWithAbsolutePositions()
    {
        var text = "s = f\"a{name}b{other!r:>{w}}\"\n";
        var m = Parse(text);
        var names = new List<NameNode>();
        void Collect(Node n)
        {
            if (n is NameNode nn)
            {
                names.Add(nn);
            }

            foreach (var c in n.Children())
            {
                if (c is not null)
                {
                    Collect(c);
                }
            }
        }

        Collect(m);
        var byName = names.ToDictionary(n => n.Value, n => n.Start);
        await Assert.That(byName["name"]).IsEqualTo(text.IndexOf("name", StringComparison.Ordinal));
        await Assert.That(byName["other"]).IsEqualTo(text.IndexOf("other", StringComparison.Ordinal));
        await Assert.That(byName["w"]).IsEqualTo(text.IndexOf("{w}", StringComparison.Ordinal) + 1);
    }

    [Test]
    public async Task RecoversFromSyntaxErrors()
    {
        var m = Parse("x = (1,\ny = 2\ndef ok():\n    pass\n");
        await Assert.That(m.SyntaxErrors).IsGreaterThan(0);
        var m2 = Parse("x = = 1\ndef ok():\n    pass\n");
        await Assert.That(m2.SyntaxErrors).IsEqualTo(1);
        await Assert.That(m2.Statements.OfType<FunctionNode>().Single().Name.Value).IsEqualTo("ok");
    }

    [Test]
    public async Task TokenizerHandlesIndentationAndContinuations()
    {
        var tokens = new Tokenizer("if x:\n    y = 1 + \\\n        2\n\n    # c\nz\n").Tokenize();
        var kinds = tokens.Select(t => t.Kind).ToList();
        await Assert.That(kinds.Count(k => k == TokenKind.Indent)).IsEqualTo(1);
        await Assert.That(kinds.Count(k => k == TokenKind.Dedent)).IsEqualTo(1);
        await Assert.That(kinds.Count(k => k == TokenKind.Newline)).IsEqualTo(3);
    }
}
