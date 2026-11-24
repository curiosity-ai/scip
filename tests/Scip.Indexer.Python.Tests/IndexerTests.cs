using Scip.Indexer.Python.Emit;
using Scip.Indexer.Python.Project;
using Scip.Indexer.Python.Semantics;

namespace Scip.Indexer.Python.Tests;

public class IndexerTests
{
    private static string NewProject(params (string Path, string Content)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-python-proj-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return dir;
    }

    private static Index IndexProject(string dir, string name = "proj", string version = "1.0", string? env = null)
    {
        var output = Path.Combine(dir, "index.scip");
        var code = PythonIndexer.Run(dir, output, new PythonIndexOptions
        {
            ProjectName = name,
            ProjectVersion = version,
            EnvironmentFile = env,
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
    public async Task EmitsMetadataDocumentsAndUtf16Positions()
    {
        var dir = NewProject(("main.py", "x = '😀'; y = x\n"));
        var index = IndexProject(dir);
        await Assert.That(index.Metadata.ToolInfo.Name).IsEqualTo("scip-python");
        await Assert.That(index.Metadata.ProjectRoot).StartsWith("file://");
        var doc = index.Documents.Single();
        await Assert.That(doc.Language).IsEqualTo("python");
        await Assert.That(doc.PositionEncoding).IsEqualTo(PositionEncoding.Utf16CodeUnitOffsetFromLineStart);
        // `y = x` : the emoji is two UTF-16 code units
        var xRef = doc.Occurrences.Single(o => o.Symbol.EndsWith("main/x.") && (o.SymbolRoles & (int)SymbolRole.Definition) == 0);
        await Assert.That(xRef.Range.ToArray()).IsEquivalentTo(new[] { 0, 14, 15 });
    }

    [Test]
    public async Task SymbolsAreValidScipSymbols()
    {
        var dir = NewProject(
            ("pkg/__init__.py", ""),
            ("pkg/mod.py", "class A:\n    def m(self):\n        self.v = 1\n        return [i for i in range(3)]\n\ndef f(a, *, b=1): return lambda q: q\n"),
            ("main.py", "from pkg.mod import A, f\nimport pkg.mod\nA().m()\nprint(pkg.mod.f)\n"));
        var index = IndexProject(dir, "my pkg", "0.1 beta");
        foreach (var o in index.Documents.SelectMany(d => d.Occurrences))
        {
            // `local N(param)` of lambda parameters is a deliberate upstream quirk; everything else must parse
            if (o.Symbol.StartsWith("local ", StringComparison.Ordinal) && o.Symbol.Contains('('))
            {
                continue;
            }

            await Assert.That(SymbolParser.TryParse(o.Symbol, out _, out _)).IsTrue();
        }

        await Assert.That(index.Documents.SelectMany(d => d.Occurrences).Any(o => o.Symbol == "scip-python python my  pkg 0.1  beta `pkg.mod`/A#m().")).IsTrue();
    }

    [Test]
    public async Task ExternalSymbolsForStdlibAndBuiltinsAreEmitted()
    {
        var dir = NewProject(("main.py", "import os\nfrom typing import Optional\nprint(int(os.sep), Optional)\n"));
        var index = IndexProject(dir);
        var externals = index.ExternalSymbols.Select(s => s.Symbol).ToList();
        await Assert.That(externals).Contains("scip-python python python-stdlib 3.12 os/__init__:");
        await Assert.That(externals).Contains("scip-python python python-stdlib 3.12 builtins/int#");
        await Assert.That(externals).Contains("scip-python python python-stdlib 3.12 builtins/print().");
        await Assert.That(externals).Contains("scip-python python python-stdlib 3.12 typing/Optional.");
    }

    [Test]
    public async Task RelationshipsForOverridesAndBaseClasses()
    {
        var dir = NewProject(("main.py", "class Base:\n    def run(self): pass\n\nclass Child(Base):\n    def run(self): pass\n    def __init__(self): pass\n"));
        var doc = IndexProject(dir).Documents.Single();
        var child = doc.Symbols.Single(s => s.Symbol.EndsWith("main/Child#", StringComparison.Ordinal));
        await Assert.That(child.Relationships.Single().Symbol).EndsWith("main/Base#");
        var run = doc.Symbols.Single(s => s.Symbol.EndsWith("main/Child#run().", StringComparison.Ordinal));
        await Assert.That(run.Relationships.Single().Symbol).EndsWith("main/Base#run().");
        await Assert.That(run.Relationships.Single().IsImplementation).IsTrue();
        var init = doc.Symbols.Single(s => s.Symbol.EndsWith("main/Child#__init__().", StringComparison.Ordinal));
        await Assert.That(init.Relationships.Count).IsEqualTo(0);
    }

    [Test]
    public async Task SelfAttributesAndInstanceMembersResolve()
    {
        var dir = NewProject(("main.py", "class A:\n    def __init__(self):\n        self.x = 1\n    def get(self):\n        return self.x\n\na = A()\na.get()\nprint(a.x)\n"));
        var doc = IndexProject(dir).Documents.Single();
        var defs = doc.Occurrences.Where(o => o.Symbol.EndsWith("main/A#x.", StringComparison.Ordinal)).ToList();
        await Assert.That(defs.Count).IsEqualTo(3);
        await Assert.That(defs.Count(o => (o.SymbolRoles & (int)SymbolRole.Definition) != 0)).IsEqualTo(1);
        await Assert.That(doc.Occurrences.Count(o => o.Symbol.EndsWith("main/A#get().", StringComparison.Ordinal))).IsEqualTo(2);
    }

    [Test]
    public async Task ImportsOfProjectSiteAndUnresolvedModules()
    {
        var site = NewProject(
            ("site/libx/__init__.py", "def helper(): pass\nclass Thing: pass\n"),
            ("site/libx-2.0.dist-info/METADATA", "Metadata-Version: 2.1\nName: libx\nVersion: 2.0\n\nbody"),
            ("site/libx-2.0.dist-info/RECORD", "libx/__init__.py,sha256=abc,10\nlibx/__pycache__/x.pyc,,\n"));
        var env = Path.Combine(site, "env.json");
        File.WriteAllText(env, "[{\"name\":\"libx\",\"version\":\"2.0\",\"files\":[\"libx/__init__.py\"]}]");
        var dir = NewProject(("main.py", "import libx\nimport missing_mod\nfrom libx import helper, Thing\nlibx.helper()\nhelper()\n"));
        var index = IndexProject(dir, env: env);
        var symbols = index.Documents.Single().Occurrences.Select(o => o.Symbol).ToList();
        // libx is not on disk in the project, so it resolves only through the environment metadata (no source needed for the module symbol)
        await Assert.That(symbols).Contains("scip-python python libx 2.0 libx/__init__:");
        await Assert.That(symbols.Any(s => s == "local 0")).IsTrue(); // the unresolved import
    }

    [Test]
    public async Task EnvironmentIsReadNativelyFromDistInfo()
    {
        var site = NewProject(
            ("libx-2.0.dist-info/METADATA", "Metadata-Version: 2.1\nName: LibX\nVersion: 2.0\n\n"),
            ("libx-2.0.dist-info/RECORD", "libx/__init__.py,sha256=a,1\nlibx/sub.pyi,,\nlibx/data.json,,\n../../bin/x,,\n"));
        var env = PythonEnvironment.FromSitePackages([site]);
        await Assert.That(env.Packages.Count).IsEqualTo(1);
        await Assert.That(env.Packages[0].Name).IsEqualTo("LibX");
        await Assert.That(env.Packages[0].Files.Count).IsEqualTo(2);
        await Assert.That(env.GetPackageForModule("libx")?.Version).IsEqualTo("2.0");
        await Assert.That(env.GetPackageForModule("LibX.sub")?.Version).IsEqualTo("2.0");
        await Assert.That(env.GetPackageForModule("other")).IsNull();
    }

    /// <summary>Port of upstream's <c>testAnnotatedVariables</c> (symbol shapes only; documentation is not asserted).</summary>
    [Test]
    public async Task AnnotatedVariablesGetExpectedSymbols()
    {
        const string source = "initialized: int = 1\nuninitialized: int\nprint(initialized, uninitialized)\nclass Base:\n    field: int = 2\n    pending: int\n    def method(self):\n        method_local: int = 3\n        return method_local\nclass Child(Base):\n    pass\ndef function():\n    function_local: int = 4\n    return function_local\nobj = Child()\nobj.method()\nmodule_assigned = 5\nmodule_assigned: int\ndef assigned_first():\n    local_assigned = 6\n    local_assigned: int\n    return local_assigned\nclass AssignedFirst:\n    class_assigned = 7\n    class_assigned: int\n";
        var dir = NewProject(("main.py", source));
        var doc = IndexProject(dir, "annotations", "1.0").Documents.Single();
        foreach (var (line, expected) in new[]
        {
            (0, "main/initialized.initialized."), (1, "main/uninitialized.uninitialized."), (4, "main/Base#field."), (5, "main/Base#pending."),
            (7, "local "), (12, "local "), (16, "main/module_assigned."), (19, "local "), (23, "main/AssignedFirst#class_assigned."),
        })
        {
            var def = doc.Occurrences.First(o => o.Range[0] == line && o.Range.Count == 3 && o.Range[2] > o.Range[1] && (o.SymbolRoles & (int)SymbolRole.Definition) != 0);
            if (expected == "local ")
            {
                await Assert.That(def.Symbol).StartsWith("local ");
            }
            else
            {
                await Assert.That(def.Symbol).EndsWith(expected);
            }
        }

        await Assert.That(doc.Occurrences.Any(o => o.Range[0] == 15 && o.Symbol.EndsWith("main/Base#method().", StringComparison.Ordinal))).IsTrue();
        await Assert.That(doc.Occurrences.Any(o => o.Symbol.EndsWith("main/Child#method().", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task MissingVersionFailsLikeUpstream()
    {
        var dir = NewProject(("main.py", "x = 1\n"));
        var code = PythonIndexer.Run(dir, Path.Combine(dir, "i.scip"), new PythonIndexOptions { InferVersionFromCommit = false, Quiet = true });
        await Assert.That(code).IsEqualTo(1);
    }

    [Test]
    public async Task NamespaceOptionPrefixesModules()
    {
        var dir = NewProject(("main.py", "def f(): pass\n"));
        var output = Path.Combine(dir, "o.scip");
        PythonIndexer.Run(dir, output, new PythonIndexOptions { ProjectName = "p", ProjectVersion = "1", ProjectNamespace = "ns", InferVersionFromCommit = false, Quiet = true });
        var index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
        await Assert.That(index.Documents.Single().Occurrences.Any(o => o.Symbol == "scip-python python p 1 `ns.main`/f().")).IsTrue();
    }

    [Test]
    [Arguments("", null, null)]
    [Arguments("[project]\nname = \"abc\"", "abc", null)]
    [Arguments("[tool.poetry]\nname = \"abc\"", "abc", null)]
    [Arguments("[project]\nversion = \"16.05\"\n[tool.poetry]\nversion = \"ignored\"", null, "16.05")]
    [Arguments("[project]\nname = \"abc\"\n[tool.poetry]\nversion = \"16.05\"", "abc", "16.05")]
    [Arguments("[tool.poetry]\nname = false\nversion = {}", null, null)]
    [Arguments("[tool]\npoetry = { name = \"abc\", version = \"16.05\" }", "abc", "16.05")]
    public async Task PyprojectInference(string toml, string? name, string? version)
    {
        var dir = NewProject(("pyproject.toml", toml));
        var cfg = ProjectConfig.Load(dir);
        await Assert.That(cfg.Name).IsEqualTo(name);
        await Assert.That(cfg.Version).IsEqualTo(version);
    }

    [Test]
    public async Task SymbolNamesMatchUpstreamEscaping()
    {
        var names = new SymbolNames(new Dictionary<string, string>());
        var pkg = new PythonPackage("package with space", "0.1", []);
        await Assert.That(names.ModuleInit(pkg, "a.b")).IsEqualTo("scip-python python package  with  space 0.1 `a.b`/__init__:");
        await Assert.That(SymbolNames.Parameter(SymbolNames.Method(names.Module(new PythonPackage("", "", []), "m"), "f"), "x"))
            .IsEqualTo("scip-python python . . m/f().(x)");
        await Assert.That(SymbolNames.Local(7)).IsEqualTo("local 7");
    }
}
