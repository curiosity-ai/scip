// Runs the structure test cases of abaplint (packages/core/test/abap/structures/*.ts, MIT, see
// THIRD-PARTY-NOTICES.md of the indexer), extracted into Data/structure-tests.json, with the semantics of structureType in
// test/abap/_utils.ts; and the parser cases of test/abap/structure_errors.ts.
using System.Text.Json;
using ScipSharp.Indexer.Abap;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Structures.Structures;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Tests;

public class StructureSuiteTests
{
    private sealed class Case
    {
        public string abap { get; set; } = "";
    }

    private sealed class Group
    {
        public string file { get; set; } = "";
        public string structure { get; set; } = "";
        public List<Case> cases { get; set; } = [];
    }

    private static readonly Lazy<List<Group>> groups = new(() =>
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "structure-tests.json");
        return JsonSerializer.Deserialize<List<Group>>(File.ReadAllText(path))!;
    });

    public static IEnumerable<string> Files() =>
        groups.Value.Select(g => g.file).Distinct();

    private static ABAPFile Parse(string abap, string filename = "zfoo.prog.abap")
    {
        var file = new MemoryFile(filename, abap);
        return new ABAPParser().Parse([file]).Output[0];
    }

    private static IStructure StructureFor(string name)
    {
        var type = typeof(IStructure).Assembly.GetType("ScipSharp.Indexer.Abap.Structures.Structures." + name)
            ?? throw new InvalidOperationException("structure class not found: " + name);
        return (IStructure)Activator.CreateInstance(type)!;
    }

    [Test]
    public async Task AllGroupsLoaded()
    {
        await Assert.That(groups.Value.Count).IsEqualTo(29);
        await Assert.That(groups.Value.Sum(g => g.cases.Count)).IsEqualTo(114);
    }

    [Test]
    [MethodDataSource(nameof(Files))]
    public async Task Structures(string file)
    {
        var failures = new List<string>();
        foreach (var g in groups.Value.Where(g => g.file == file))
        {
            var expected = StructureFor(g.structure);
            foreach (var c in g.cases)
            {
                // note that parsing will also trigger the structure matches
                var statements = Parse(c.abap).GetStatements().ToList();
                var length = statements.Count;
                var match = expected.GetMatcher().Run(statements.ToList(), new StructureNode(expected));

                if (match.ErrorDescription != "" || length != statements.Count || match.Error || match.Matched.Count != length)
                {
                    failures.Add(g.structure + ": " + c.abap + " => error=" + match.Error + " \"" + match.ErrorDescription
                        + "\" matched " + match.Matched.Count + " of " + length);
                }
            }
        }
        await Assert.That(failures.Count).IsEqualTo(0).Because(string.Join("\n", failures));
    }

    // test/abap/structure_errors.ts, "Structure, test error messages, parser"
    [Test]
    [Arguments("ENDIF.", "Unexpected ENDIF")]
    [Arguments("IF foo = bar.", "Expected ENDIF")]
    [Arguments("IF foo = bar. ENDIF. ENDWHILE.", "Unexpected ENDWHILE")]
    [Arguments("CLASS zfoo DEFINITION. ENDCLASS.", "")]
    [Arguments("IF foo = bar. ENDIF. WRITE asdf.", "")]
    [Arguments("IF foo = bar. IF moo = boo.", "Expected ENDIF")]
    [Arguments("IF foo = bar. IF moo = boo. ENDIF.", "Expected ENDIF")]
    [Arguments("CLASS zfoo DEFINITION. PUBLIC SECTION. ENDCLASS.", "")]
    [Arguments("DO 2 TIMES. ENDDO.", "")]
    [Arguments("DO 2 TIMES.\n*comment\nENDDO.", "")]
    [Arguments("DO 2 TIMES.", "Expected ENDDO")]
    [Arguments("CLASS zfoo DEFINITION. PUBLIC SECTION. WRITE asdf. ENDCLASS.", "Expected ENDCLASS")]
    public async Task ErrorMessages(string abap, string error)
    {
        var issues = new ABAPParser().Parse([new MemoryFile("zfoo.prog.abap", abap)]).Issues
            .Where(i => i.GetKey() == "structure").ToList();
        if (error == "")
        {
            await Assert.That(issues.Count).IsEqualTo(0);
        }
        else
        {
            await Assert.That(issues.Count).IsEqualTo(1);
            await Assert.That(issues[0].GetMessage()).IsEqualTo(error);
        }
    }

    // test/abap/structure_errors.ts, "Structure, test empty class"
    [Test]
    public async Task EmptyClassFile()
    {
        var issues = new ABAPParser().Parse([new MemoryFile("zcl_foo.clas.abap", "")]).Issues;
        await Assert.That(issues.Any(i => i.GetMessage() == "Expected CLASSDEFINITION")).IsTrue();
    }

    [Test]
    [Arguments("zcl_foo.clas.abap", "ClassGlobal")]
    [Arguments("zif_foo.intf.abap", "InterfaceGlobal")]
    [Arguments("zfoo.prog.screen_0100.abap", "DynproLogic")]
    [Arguments("SAPLZFOO.FUGR.SCREEN_0100.ABAP", "DynproLogic")]
    [Arguments("zcl_foo.clas.locals_imp.abap", "Any")]
    [Arguments("zfoo.prog.abap", "Any")]
    public async Task RootStructurePerFileType(string filename, string expected)
    {
        await Assert.That(ScipSharp.Indexer.Abap.Structures.StructureParser.FindStructureForFile(filename).GetType().Name).IsEqualTo(expected);
    }

    [Test]
    public async Task ArtifactsListsAllStructures()
    {
        var list = ArtifactsABAP.GetStructures();
        await Assert.That(list.Count).IsEqualTo(59);
        await Assert.That(list[0].GetType().Name).IsEqualTo("Any");
    }
}
