using ScipSharp.Indexer.Clang;

namespace ScipSharp.Indexer.Clang.Tests;

public class PreprocessorTests
{
    private static List<string> Preprocess(string source, params (string Name, string Value)[] defines)
    {
        var dir = Directory.CreateTempSubdirectory("scip-clang-pp").FullName;
        var path = Path.Combine(dir, "t.cc");
        File.WriteAllText(path, source);
        var pp = new Preprocessor(new FileTable(dir), [], defines.Select(static d => new KeyValuePair<string, string?>(d.Name, d.Value)));
        pp.Run(path, []);
        return pp.Output.Select(static t => t.Text).ToList();
    }

    [Test]
    public async Task EvaluatesConditionals()
    {
        var toks = Preprocess("#if defined(A) && (B >= 2 || 0)\nyes\n#elif 1\nelif\n#else\nno\n#endif\n", ("A", "1"), ("B", "3"));
        await Assert.That(toks).IsEquivalentTo(new[] { "yes" });
        await Assert.That(Preprocess("#ifdef X\nx\n#else\nnot_x\n#endif")).IsEquivalentTo(new[] { "not_x" });
    }

    [Test]
    public async Task ExpandsObjectAndFunctionLikeMacros()
    {
        var toks = Preprocess("#define ADD(a, b) ((a) + (b))\n#define N 3\nint v = ADD(N, 4);\n");
        await Assert.That(string.Concat(toks)).IsEqualTo("intv=((3)+(4));");
    }

    [Test]
    public async Task SupportsStringizeAndPaste()
    {
        var toks = Preprocess("#define S(x) #x\n#define C(a, b) a##b\nS(hi) C(foo, 1)\n");
        await Assert.That(toks).IsEquivalentTo(new[] { "\"hi\"", "foo1" });
    }

    [Test]
    public async Task LexerReportsUtf8ByteColumns()
    {
        var toks = Lexer.Lex(System.Text.Encoding.UTF8.GetBytes("/* é */ int x;"), 0);
        await Assert.That(toks[0].Col).IsEqualTo(9); // "é" is two bytes
    }
}
