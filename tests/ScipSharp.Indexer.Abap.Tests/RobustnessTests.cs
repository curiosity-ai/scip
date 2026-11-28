using System.Text;

namespace ScipSharp.Indexer.Abap.Tests;

/// <summary>
/// Inputs that made the indexer hang (exponential or quadratic work) or crash (stack overflow). Each test indexes a
/// generated project and must finish quickly; the stack tests run on a small stack, as a library caller's thread may
/// have, so that the guards and not the CLI's large stack are what is tested.
/// </summary>
public class RobustnessTests
{
    private const string ProgXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <abapGit version="v1.0.0" serializer="LCL_OBJECT_PROG" serializer_version="v1.0.0"><asx:abap xmlns:asx="http://www.sap.com/abapxml" version="1.0"><asx:values><PROGDIR><NAME>$NAME</NAME><SUBC>$SUBC</SUBC></PROGDIR></asx:values></asx:abap></abapGit>
        """;

    private static string NewProject()
    {
        var root = Path.Combine(Path.GetTempPath(), "scip-abap-robustness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        return root;
    }

    private static void AddProgram(string root, string name, string source, bool include = false)
    {
        File.WriteAllText(Path.Combine(root, "src", name + ".prog.abap"), source);
        File.WriteAllText(Path.Combine(root, "src", name + ".prog.xml"), ProgXml.Replace("$NAME", name.ToUpperInvariant()).Replace("$SUBC", include ? "I" : "1"));
    }

    /// <summary>Indexes the project on a thread with the given stack, failing if that takes longer than the limit.</summary>
    private static Index IndexWithin(string root, TimeSpan limit, int stackSize = 1024 * 1024)
    {
        Index? index = null;
        Exception? error = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    index = IndexerTests.Build(root);
                }
#pragma warning disable CA1031
                catch (Exception e)
#pragma warning restore CA1031
                {
                    error = e;
                }
            },
            stackSize)
        { IsBackground = true };
        thread.Start();
        try
        {
            if (!thread.Join(limit))
            {
                throw new TimeoutException($"indexing took longer than {limit}");
            }
            if (error != null)
            {
                throw new InvalidOperationException("indexing failed", error);
            }
            return index!;
        }
        finally
        {
            if (!thread.IsAlive)
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static int Occurrences(Index index, string file) =>
        index.Documents.Single(d => d.RelativePath == "src/" + file).Occurrences.Count;

    [Test]
    public async Task IncludesIncludingTheSameIncludesAreNotWalkedPerPath()
    {
        // zmain includes zi0a and zi0b, each of zi<n>a and zi<n>b includes zi<n+1>a and zi<n+1>b: 2^30 paths
        var root = NewProject();
        const int depth = 30;
        AddProgram(root, "zmain", "REPORT zmain.\nINCLUDE zi0a.\nINCLUDE zi0b.\n");
        for (var i = 0; i < depth; i++)
        {
            foreach (var side in "ab")
            {
                var next = i + 1 < depth ? $"INCLUDE zi{i + 1}a.\nINCLUDE zi{i + 1}b.\n" : "";
                AddProgram(root, $"zi{i}{side}", $"DATA v{i}{side} TYPE i.\n" + next, include: true);
            }
        }

        var index = IndexWithin(root, TimeSpan.FromSeconds(30), stackSize: 16 * 1024 * 1024);
        await Assert.That(Occurrences(index, $"zi{depth - 1}b.prog.abap")).IsGreaterThan(0);
    }

    [Test]
    public async Task UnknownStatementOverManyLinesIsNotReparsedPerLine()
    {
        var root = NewProject();
        var source = new StringBuilder("REPORT zstress.\n");
        for (var i = 0; i < 4000; i++)
        {
            source.Append("foo bar").Append(i).Append(" baz ( qux\n");
        }
        AddProgram(root, "zstress", source.Append(".\n").ToString());

        var index = IndexWithin(root, TimeSpan.FromSeconds(30), stackSize: 16 * 1024 * 1024);
        await Assert.That(index.Documents.Count).IsGreaterThan(0);
    }

    [Test]
    [Arguments("arith")]
    [Arguments("calls")]
    [Arguments("cond")]
    [Arguments("parens")]
    [Arguments("chain")]
    [Arguments("value")]
    [Arguments("template")]
    [Arguments("strings")]
    public async Task VeryLongOrDeepStatementDoesNotOverflowTheStack(string shape)
    {
        const int n = 20000;
        var expression = shape switch
        {
            // shorter: matching a chain of calls is quadratic in its length (every prefix is a match that is wrapped
            // into a node), harmless at real lengths but slow at 20000
            "chain" => "cl_x=>m( )" + string.Concat(Enumerable.Repeat("->m( )", 3000)) + "->a",
            "value" => string.Concat(Enumerable.Repeat("VALUE #( ( ", n)) + "1" + string.Concat(Enumerable.Repeat(" ) )", n)),
            "template" => string.Concat(Enumerable.Repeat("|{ ", n)) + "b" + string.Concat(Enumerable.Repeat(" }|", n)),
            "strings" => string.Join(" && ", Enumerable.Range(0, n).Select(i => $"'x{i}'")),
            "arith" => string.Join(" + ", Enumerable.Range(0, n).Select(i => $"b * {i}")),
            "calls" => string.Concat(Enumerable.Repeat("cl_x=>m( ", n)) + "b" + string.Concat(Enumerable.Repeat(" )", n)),
            "cond" => string.Concat(Enumerable.Repeat("COND #( WHEN a = 1 THEN ", n)) + "1" + string.Concat(Enumerable.Repeat(" ELSE 2 )", n)),
            _ => string.Concat(Enumerable.Repeat("( ", n)) + "b" + string.Concat(Enumerable.Repeat(" )", n)),
        };
        var root = NewProject();
        AddProgram(root, "zstress", $"REPORT zstress.\nDATA: a TYPE i, b TYPE i.\na = {expression}.\na = b.\n");

        // the statement is too deep for a 1 MB stack and is left unparsed; the rest of the program is indexed
        var index = IndexWithin(root, TimeSpan.FromSeconds(60));
        await Assert.That(Occurrences(index, "zstress.prog.abap")).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    [Arguments("if")]
    [Arguments("where")]
    public async Task VeryLongConditionDoesNotOverflowTheStack(string shape)
    {
        const int n = 20000;
        var conditions = string.Join(" AND ", Enumerable.Range(0, n).Select(i => i % 2 == 0 ? $"a = {i}" : $"( b = {i} OR a <> b )"));
        var statement = shape == "if"
            ? $"IF {conditions}.\n  a = 1.\nENDIF.\n"
            : $"SELECT * FROM t000 INTO TABLE @DATA(r) WHERE {conditions.Replace("a ", "mandt ", StringComparison.Ordinal)}.\n";
        var root = NewProject();
        AddProgram(root, "zstress", $"REPORT zstress.\nDATA: a TYPE i, b TYPE i.\n{statement}a = b.\n");

        var index = IndexWithin(root, TimeSpan.FromSeconds(60));
        await Assert.That(Occurrences(index, "zstress.prog.abap")).IsGreaterThanOrEqualTo(4);
    }

    [Test]
    public async Task DeeplyNestedBlocksDoNotOverflowTheStack()
    {
        const int n = 3000;
        var root = NewProject();
        AddProgram(root, "zstress", "REPORT zstress.\nDATA a TYPE i.\n"
            + string.Concat(Enumerable.Repeat("IF a = 1.\n", n)) + "a = 2.\n" + string.Concat(Enumerable.Repeat("ENDIF.\n", n)));

        var index = IndexWithin(root, TimeSpan.FromSeconds(60));
        await Assert.That(index.Documents.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task ManyFormsAndDeclarationsAreIndexedInLinearTime()
    {
        // 20000 FORMs each calling the previous one took ~50 s (lookups by scanning all FORMs and all statements)
        const int n = 20000;
        var root = NewProject();
        var source = new StringBuilder("REPORT zstress.\n");
        for (var i = 0; i < n; i++)
        {
            source.Append($"FORM f{i}.\n  DATA v{i} TYPE i.\n  PERFORM f{Math.Max(0, i - 1)}.\nENDFORM.\n");
        }
        AddProgram(root, "zstress", source.ToString());

        var index = IndexWithin(root, TimeSpan.FromSeconds(30), stackSize: 16 * 1024 * 1024);
        await Assert.That(Occurrences(index, "zstress.prog.abap")).IsGreaterThanOrEqualTo(3 * n);
    }
}
