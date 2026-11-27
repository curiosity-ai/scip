// Ported from abaplint packages/core/src/abap/types/method_definitions.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Structures.Structures;
using ScipSharp.Indexer.Abap.Syntax;

namespace ScipSharp.Indexer.Abap.Types;

public class MethodDefinitions : IMethodDefinitions
{
    private readonly Dictionary<string, IMethodDefinition> all = new Dictionary<string, IMethodDefinition>();

    public MethodDefinitions(StructureNode node, SyntaxInput input)
    {
        this.all = new Dictionary<string, IMethodDefinition>();
        this.Parse(node, input);
    }

    public IEnumerable<IMethodDefinition> GetAll()
    {
        foreach (var a in this.all.Keys.ToList())
        {
            yield return this.all.GetValueOrDefault(a);
        }
    }

    public IMethodDefinition? GetByName(string? name)
    {
        if (name == null)
        {
            return null;
        }

        return this.all.GetValueOrDefault(JsUtils.ToUpper(name));
    }

    ///////////////////////
    private void ParseInterface(StructureNode node, SyntaxInput input)
    {
        var defs = node.FindAllStatements<MethodDef>();
        foreach (var def in defs)
        {
            this.Add(def, Visibility.Public, input);
        }
    }

    private void Parse(StructureNode node, SyntaxInput input)
    {
        var idef = node.FindDirectStructure<global::ScipSharp.Indexer.Abap.Structures.Structures.Interface>();
        if (idef != null)
        {
            this.ParseInterface(node, input);
            return;
        }

        var cdef = node.FindDirectStructure<global::ScipSharp.Indexer.Abap.Structures.Structures.ClassDefinition>();
        if (!(cdef != null))
        {
            throw new Exception("MethodDefinitions, expected ClassDefinition as part of input node");
        }

        var pri = cdef.FindDirectStructure<PrivateSection>();
        foreach (var def in pri?.FindAllStatements<MethodDef>() ?? new List<StatementNode>())
        {
            global::ScipSharp.Indexer.Abap.Statements.StatementParser.Current = def.GetTokens();
            this.Add(def, Visibility.Private, input);
        }

        var pro = node.FindDirectStructure<ProtectedSection>();
        foreach (var def in pro?.FindAllStatements<MethodDef>() ?? new List<StatementNode>())
        {
            global::ScipSharp.Indexer.Abap.Statements.StatementParser.Current = def.GetTokens();
            this.Add(def, Visibility.Protected, input);
        }

        var pub = node.FindDirectStructure<PublicSection>();
        foreach (var def in pub?.FindAllStatements<MethodDef>() ?? new List<StatementNode>())
        {
            global::ScipSharp.Indexer.Abap.Statements.StatementParser.Current = def.GetTokens();
            this.Add(def, Visibility.Public, input);
        }
    }

    private void Add(StatementNode def, Visibility visibility, SyntaxInput input)
    {
        try
        {
            var m = new MethodDefinition(def, visibility, input);
            this.all[JsUtils.ToUpper(m.GetName())] = m;
        }
        catch (Exception e)
        {
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, def.GetFirstToken(), e.Message));
        }
    }
}
