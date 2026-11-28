using System.Text;
using ScipSharp.Indexer.Python.Syntax;

namespace ScipSharp.Indexer.Python.Tests;

/// <summary>Pathological inputs that used to hang the indexer, take exponential time or overflow the stack.</summary>
public class HardeningTests
{
    private static string NewProject(params (string Path, string Content)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-python-hard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var (path, content) in files)
        {
            File.WriteAllText(Path.Combine(dir, path), content);
        }

        return dir;
    }

    private static Index IndexProject(string dir)
    {
        var output = Path.Combine(dir, "index.scip");
        var code = PythonIndexer.Run(dir, output, new PythonIndexOptions
        {
            ProjectName = "proj",
            ProjectVersion = "1.0",
            PythonVersion = "3.12",
            InferVersionFromCommit = false,
            Quiet = true,
        });
        if (code != 0)
        {
            throw new InvalidOperationException("exit " + code);
        }

        return Index.Parser.ParseFrom(File.ReadAllBytes(output));
    }

    [Test]
    public async Task CharactersThatCannotContinueANameDoNotStallTheTokenizer()
    {
        // `²` (No), an emoji (surrogate pair) and U+200B (Cf) start but cannot continue a name: this used to loop forever.
        var m = Parser.Parse("a = 2\u00b2\nb = \U0001F600\nc = \u200b1\nd = 1\n");
        await Assert.That(m.SyntaxErrors).IsEqualTo(3);
        await Assert.That(m.Statements.OfType<AssignmentNode>().Any(a => a.Targets[0] is NameNode { Value: "d" })).IsTrue();
    }

    [Test]
    public async Task LongChainsAreIndexedWithoutDeepRecursion()
    {
        const int n = 3_000;
        var text = new StringBuilder();
        text.Append("v = 1\nx = ").AppendJoin(" + ", Enumerable.Repeat("v", n)).Append('\n');
        text.Append("y = v").Append(string.Concat(Enumerable.Repeat(".b()", n))).Append('\n');
        text.Append("def f(p=").AppendJoin(" + ", Enumerable.Repeat("1", n)).Append("):\n    pass\n");
        text.Append("if v:\n    pass\n");
        for (var i = 0; i < 1_000; i++)
        {
            text.Append("elif v == ").Append(i).Append(":\n    pass\n");
        }

        text.Append("last = v\n");
        var index = IndexProject(NewProject(("m.py", text.ToString())));
        var doc = index.Documents.Single();
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.EndsWith("m/last.", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task DeeplyNestedStatementIsASyntaxErrorNotACrash()
    {
        var m = Parser.Parse("x = " + new string('(', 50_000) + "1" + new string(')', 50_000) + "\ny = -" + new string('-', 50_000) + "1\nz = 1\n");
        await Assert.That(m.SyntaxErrors).IsEqualTo(2);
        await Assert.That(m.Statements.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ClassWhoseBaseIsAMemberOfItselfTerminates()
    {
        var index = IndexProject(NewProject(("m.py", "class A(A.x):\n    pass\nclass B(C.y):\n    pass\nclass C(B.z):\n    pass\nA.q\nB().r\n")));
        await Assert.That(index.Documents.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ReExportChainsWithAlternativesAreNotExponential()
    {
        // Each module re-exports X through two alternative imports: 2^n evaluation paths before the work limit.
        const int n = 20;
        var files = new List<(string, string)>();
        for (var i = 0; i < n; i++)
        {
            files.Add(($"m{i}.py", $"try:\n    from m{i + 1} import X as _X\nexcept ImportError:\n    from m{i + 1} import Y as _X\nX = _X\nY = _X\n"));
        }

        files.Add(($"m{n}.py", "X = None\nY = None\n"));
        files.Add(("main.py", "from m0 import X\nX.foo\nX()\n"));
        var index = IndexProject(NewProject([.. files]));
        await Assert.That(index.Documents.Count).IsEqualTo(n + 2);
    }

    [Test]
    public async Task ManyAssignmentsAndCallsOfALargeFunctionStayLinear()
    {
        var text = new StringBuilder("def f():\n");
        for (var i = 0; i < 2000; i++)
        {
            text.Append("    x").Append(i).Append(" = ").Append(i).Append('\n');
        }

        text.Append("    return None\na = 1\n");
        for (var i = 0; i < 2000; i++)
        {
            text.Append("a = a.b\nf().m").Append(i).Append('\n');
        }

        var index = IndexProject(NewProject(("m.py", text.ToString())));
        await Assert.That(index.Documents.Count).IsEqualTo(1);
    }
}
