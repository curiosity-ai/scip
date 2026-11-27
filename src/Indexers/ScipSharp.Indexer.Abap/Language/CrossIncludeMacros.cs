// Ported from abaplint packages/core/src/abap/cross_include_macros.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Files;
using ScipSharp.Indexer.Abap.Objects;
using ScipSharp.Indexer.Abap.Utils;

namespace ScipSharp.Indexer.Abap;

// Macros can be defined in one include and used in a sibling include, connected
// via the main program. Objects are parsed in isolation, so the sibling include
// initially ends up with Unknown statements. This second pass finds the main
// program(s) for such includes, collects the macro definitions visible there,
// and re-parses the include with the macro names as extra global macros.
public class CrossIncludeMacros
{
    private readonly IRegistry reg;

    public CrossIncludeMacros(IRegistry reg)
    {
        this.reg = reg;
    }

    public void Run()
    {
        this.ReparseCyclicPrograms();

        var candidates = this.FindCandidates();
        if (candidates.Count == 0)
        {
            return;
        }

        var graph = new IncludeGraph(this.reg);
        var config = this.reg.GetConfig();
        var globalMacros = config.GetSyntaxSetttings().GlobalMacros ?? new List<string>();

        foreach (var prog in candidates)
        {
            var mains = this.FindMains(graph, prog);
            if (mains.Count == 0)
            {
                continue;
            }

            var expand = new ExpandMacros(globalMacros, config.GetRelease(), this.reg, config.GetLanguageVersion());
            foreach (var main in mains)
            {
                foreach (var file in main.GetABAPFiles())
                {
                    // find() follows INCLUDE statements, so this collects macros from the full include chain
                    expand.Find(new List<StatementNode>([.. file.GetStatements()]), file, false);
                }
            }

            var macroNames = expand.ListMacroNames();
            if (this.MatchesUnknown(prog, new HashSet<string>(macroNames)))
            {
                prog.SetDirty();
                prog.Parse(config.GetRelease(), macroNames, this.reg, config.GetLanguageVersion());
            }
        }
    }

    //////////////////////////////
    // A program or include in an INCLUDE cycle can be parsed while the include defining its macros is still
    // being parsed, depending on the order the files were added. Everything is parsed now, so parse it again.
    private void ReparseCyclicPrograms()
    {
        var config = this.reg.GetConfig();
        foreach (var obj in this.reg.GetObjects())
        {
            if (obj is global::ScipSharp.Indexer.Abap.Objects.Program && this.HasUnknown(((global::ScipSharp.Indexer.Abap.Objects.Program)obj)) && this.IsInIncludeCycle(((global::ScipSharp.Indexer.Abap.Objects.Program)obj)))
            {
                ((global::ScipSharp.Indexer.Abap.Objects.Program)obj).SetDirty();
                ((global::ScipSharp.Indexer.Abap.Objects.Program)obj).Parse(config.GetRelease(), config.GetSyntaxSetttings().GlobalMacros, this.reg, config.GetLanguageVersion());
            }
        }
    }

    private bool HasUnknown(global::ScipSharp.Indexer.Abap.Objects.Program prog)
    {
        return prog.GetABAPFiles().Any(f => f.GetStatements().Any(s => s.Get() is Unknown));
    }

    // the include graph does not link INCLUDEs of main programs, so follow the INCLUDEs from the program
    private bool IsInIncludeCycle(global::ScipSharp.Indexer.Abap.Objects.Program prog)
    {
        var seen = new HashSet<global::ScipSharp.Indexer.Abap.Objects.Program>();
        List<global::ScipSharp.Indexer.Abap.Objects.Program> stack = new List<global::ScipSharp.Indexer.Abap.Objects.Program> { prog };
        while (stack.Count > 0)
        {
            foreach (var file in JsUtils.Pop(stack)!.GetABAPFiles())
            {
                foreach (var statement in file.GetStatements())
                {
                    if (!((statement.Get() is Include)))
                    {
                        continue;
                    }
                    var name = statement.FindDirectExpression<IncludeName>()?.ConcatTokens();
                    var included = this.reg.GetObject("PROG", name);
                    if (included == prog)
                    {
                        return true;
                    }
                    else if (included is global::ScipSharp.Indexer.Abap.Objects.Program && seen.Contains(((global::ScipSharp.Indexer.Abap.Objects.Program)included)) == false)
                    {
                        seen.Add(((global::ScipSharp.Indexer.Abap.Objects.Program)included));
                        stack.Add(((global::ScipSharp.Indexer.Abap.Objects.Program)included));
                    }
                }
            }
        }
        return false;
    }

    private List<global::ScipSharp.Indexer.Abap.Objects.Program> FindCandidates()
    {
        List<global::ScipSharp.Indexer.Abap.Objects.Program> ret = new List<global::ScipSharp.Indexer.Abap.Objects.Program>();
        foreach (var obj in this.reg.GetObjects())
        {
            if (!((obj is global::ScipSharp.Indexer.Abap.Objects.Program)) || ((global::ScipSharp.Indexer.Abap.Objects.Program)obj).IsInclude() == false)
            {
                continue;
            }
            foreach (var file in ((global::ScipSharp.Indexer.Abap.Objects.Program)obj).GetABAPFiles())
            {
                if (file.GetStatements().Any(s => s.Get() is Unknown))
                {
                    ret.Add(((global::ScipSharp.Indexer.Abap.Objects.Program)obj));
                    break;
                }
            }
        }
        return ret;
    }

    private HashSet<ABAPObject> FindMains(IncludeGraph graph, global::ScipSharp.Indexer.Abap.Objects.Program prog)
    {
        var ret = new HashSet<ABAPObject>();
        var filename = prog.GetMainABAPFile()?.GetFilename();
        foreach (var mainFilename in graph.ListMainForInclude(filename))
        {
            var file = this.reg.GetFileByName(mainFilename);
            IObject? obj = file != null ? this.reg.FindObjectForFile(file) : (IObject?)null;
            if (obj is ABAPObject)
            {
                ret.Add(((ABAPObject)obj));
            }
        }
        return ret;
    }

    private bool MatchesUnknown(global::ScipSharp.Indexer.Abap.Objects.Program prog, HashSet<string> macroNames)
    {
        foreach (var file in prog.GetABAPFiles())
        {
            foreach (var s in file.GetStatements())
            {
                if (s.Get() is Unknown)
                {
                    var name = ExpandMacros.FindName(s.GetTokens());
                    if (name != null && macroNames.Contains(JsString.ToUpper(name)))
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
