using Scip.Indexer.Php.Composer;

namespace Scip.Indexer.Php.Tests;

/// <summary>Port of upstream <c>ComposerTest</c> (the jetbrains stub path assertions are replaced by the built-in catalogue).</summary>
public class ComposerTests
{
    private static readonly string[] BuiltinClasses = ["Exception"];
    private static readonly string[] BuiltinConsts = ["DIRECTORY_SEPARATOR"];
    private static readonly string[] BuiltinFuncs = ["strlen"];
    private static readonly string[] DepClasses = ["DeepCopy\\DeepCopy", "Composer\\Autoload\\ClassLoader"];
    private static readonly string[] DepFuncs = ["DeepCopy\\deep_copy"];

    private static readonly string[] ProjectClasses =
    [
        "anon-class-123", "TestData1\\ClassA", "TestData2\\ClassC", "TestDataTests\\ClassATestCase", "TestDataTests\\ClassBTestCase",
        "TestDataTests\\EnumC", "TestDataTests\\TraitD", "TestDataTests\\InterfaceE",
    ];

    private static readonly string[] ProjectConsts = ["CONST_1", "CONST_2", "CONST_3", "TestData3\\Foo\\CONST_4", "TestData3\\Foo\\CONST_5", "CONST_6"];
    private static readonly string[] ProjectFuncs = ["anon-func-123", "fun1", "TestData3\\Foo\\fun1"];

    private static readonly string[] UnknownClasses = ["Foo\\Foo", "Foo"];
    private static readonly string[] UnknownConsts = ["Foo\\FOO", "FOO"];
    private static readonly string[] UnknownFuncs = ["Foo\\foo", "foo"];

    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "testdata", "composer");

    private static ComposerProject Create() => new(Root, "8.4.1", new PhpFs());

    private static IEnumerable<(string Ident, string Kind)> All() =>
        BuiltinClasses.Select(i => (i, "classes")).Concat(BuiltinConsts.Select(i => (i, "consts"))).Concat(BuiltinFuncs.Select(i => (i, "funcs")))
            .Concat(DepClasses.Select(i => (i, "classes"))).Concat(DepFuncs.Select(i => (i, "funcs")))
            .Concat(ProjectClasses.Select(i => (i, "classes"))).Concat(ProjectConsts.Select(i => (i, "consts"))).Concat(ProjectFuncs.Select(i => (i, "funcs")));

    [Test]
    public async Task ProjectFiles()
    {
        var files = Create().ProjectFiles;
        await Assert.That(files.Count).IsEqualTo(5);
        var expected = new[] { "bin/main", "src/file1.php", "src/file2.php", "src/ClassA.php", "tests/ClassATestCase.php" };
        for (var i = 0; i < expected.Length; i++)
        {
            await Assert.That(files[i].Replace(Path.DirectorySeparatorChar, '/')).EndsWith("testdata/composer/" + expected[i]);
        }
    }

    [Test]
    public async Task IsDependency()
    {
        var c = Create();
        foreach (var i in BuiltinClasses.Concat(BuiltinConsts).Concat(BuiltinFuncs).Concat(DepClasses).Concat(DepFuncs).Concat(UnknownClasses).Concat(UnknownConsts).Concat(UnknownFuncs))
        {
            await Assert.That(c.IsDependency(i)).IsTrue();
        }

        foreach (var i in ProjectClasses.Concat(ProjectConsts).Concat(ProjectFuncs))
        {
            await Assert.That(c.IsDependency(i)).IsFalse();
        }
    }

    [Test]
    public async Task IsConstIsClassLikeIsFunc()
    {
        var c = Create();
        var failures = new List<string>();
        foreach (var (ident, kind) in All())
        {
            if (c.IsConst(ident) != (kind == "consts") || c.IsClassLike(ident) != (kind == "classes") || c.IsFunc(ident) != (kind == "funcs"))
            {
                failures.Add($"{ident} ({kind})");
            }
        }

        if (failures.Count > 0) { throw new InvalidOperationException(string.Join(", ", failures)); }

        foreach (var i in UnknownClasses.Concat(UnknownConsts).Concat(UnknownFuncs))
        {
            await Assert.That(c.IsConst(i)).IsFalse();
            await Assert.That(c.IsClassLike(i)).IsFalse();
            await Assert.That(c.IsFunc(i)).IsFalse();
        }
    }

    [Test]
    public async Task FindFile()
    {
        var c = Create();
        foreach (var i in BuiltinClasses.Concat(BuiltinConsts).Concat(BuiltinFuncs))
        {
            await Assert.That(c.FindFile(i)).IsEqualTo("stub:" + i);
        }

        foreach (var i in DepClasses.Concat(DepFuncs))
        {
            var f = c.FindFile(i)!;
            await Assert.That(f).IsNotNull();
            await Assert.That(f).Contains("testdata" + Path.DirectorySeparatorChar + "composer" + Path.DirectorySeparatorChar + "vendor-dir");
            await Assert.That(f).EndsWith(".php");
        }

        foreach (var i in ProjectClasses.Concat(ProjectConsts).Concat(ProjectFuncs))
        {
            var f = c.FindFile(i);
            if (i.StartsWith("anon-", StringComparison.Ordinal))
            {
                await Assert.That(f).IsNull();
                continue;
            }

            if (f == null) { throw new InvalidOperationException("missing " + i); }
            await Assert.That(f).IsNotNull();
            await Assert.That(f!).DoesNotContain("vendor");
            await Assert.That(f).EndsWith(".php");
        }

        foreach (var i in UnknownClasses.Concat(UnknownConsts).Concat(UnknownFuncs))
        {
            await Assert.That(c.FindFile(i)).IsNull();
        }
    }

    [Test]
    public async Task Pkg()
    {
        var c = Create();
        foreach (var i in BuiltinClasses.Concat(BuiltinConsts).Concat(BuiltinFuncs))
        {
            await Assert.That(c.Pkg(i)).IsEqualTo(("php", "8.4.1"));
        }

        foreach (var i in DepFuncs.Append(DepClasses[0]))
        {
            var pkg = c.Pkg(i)!.Value;
            await Assert.That(pkg.Name).IsEqualTo("myclabs/deep-copy");
            await Assert.That(pkg.Version.Length).IsEqualTo(40);
        }

        await Assert.That(c.Pkg(DepClasses[1])).IsEqualTo(("composer", "dev"));
        foreach (var i in ProjectClasses.Concat(ProjectConsts).Concat(ProjectFuncs))
        {
            await Assert.That(c.Pkg(i)!.Value.Name).IsEqualTo("davidrjenni/scip-php-composer-test");
        }

        foreach (var i in UnknownClasses.Concat(UnknownConsts).Concat(UnknownFuncs))
        {
            await Assert.That(c.Pkg(i)).IsNull();
        }
    }

    [Test]
    public async Task ResolvesSymlinkedDependencies()
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-php-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "real", "x"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "real", "x", "f.txt"), "x");
            Directory.CreateSymbolicLink(Path.Combine(dir, "link"), Path.Combine(dir, "real"));
            var real = new PhpFs().Realpath(Path.Combine(dir, "link", "x", "f.txt"));
            await Assert.That(real).IsEqualTo(Path.Combine(new PhpFs().Realpath(dir)!, "real", "x", "f.txt"));
            await Assert.That(new PhpFs().Realpath(Path.Combine(dir, "nope"))).IsNull();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
