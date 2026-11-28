// Ported from abaplint packages/core/src/abap/5_syntax/spaghetti_scope.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Syntax;

public abstract class ScopeData
{
    private readonly IScopeData data;

    public ScopeData()
    {
        this.data = new IScopeData { Vars = new Dictionary<string, TypedIdentifier>(), Cdefs = new Dictionary<string, IClassDefinition>(), Idefs = new Dictionary<string, IInterfaceDefinition>(), Forms = new List<IFormDefinition>(), Types = new Dictionary<string, TypedIdentifier>(), ExtraLikeTypes = new Dictionary<string, TypedIdentifier>(), Deferred = new Dictionary<string, DeferredInformation>(), References = new List<IReference>(), SqlConversion = new List<(string fieldName, string message, AbstractToken token)>() };
    }

    public IScopeData GetData()
    {
        return this.data;
    }
}

public class SpaghettiScopeNode : ScopeData, ISpaghettiScopeNode
{
    private readonly IScopeIdentifier identifier;
    private readonly List<SpaghettiScopeNode> children;
    private readonly SpaghettiScopeNode? parent;

    public SpaghettiScopeNode(IScopeIdentifier identifier, SpaghettiScopeNode? parent)
        : base()
    {
        this.identifier = identifier;
        this.parent = parent;
        this.children = new List<SpaghettiScopeNode>();
    }

    public SpaghettiScopeNode? GetParent()
    {
        return this.parent;
    }

    // Not in abaplint: FORMs looked up by name. Every PERFORM and every FORM searched the list of all FORMs of the
    // program, quadratic in the number of FORMs. The list is changed in place elsewhere (forms are added, and replaced
    // by a resolved version), so the index catches up with what was added and is rebuilt when a lookup finds it stale.
    private Dictionary<string, int>? formIndex;
    private int formIndexed;

    /// <summary>The index in <c>GetData().Forms</c> of the first FORM with the (upper case) name here, -1 if none.</summary>
    internal int IndexOfForm(string upper)
    {
        var forms = this.GetData().Forms;
        if (this.formIndex == null || this.formIndexed > forms.Count)
        {
            this.formIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            this.formIndexed = 0;
        }
        for (; this.formIndexed < forms.Count; this.formIndexed++)
        {
            this.formIndex.TryAdd(JsUtils.ToUpper(forms[this.formIndexed].GetName()), this.formIndexed);
        }

        if (!this.formIndex.TryGetValue(upper, out var i))
        {
            return -1;
        }
        if (JsUtils.ToUpper(forms[i].GetName()) == upper)
        {
            return i;
        }

        // stale, the form at the index was replaced by one with another name: search the list and index anew
        this.formIndex = null;
        for (var j = 0; j < forms.Count; j++)
        {
            if (JsUtils.ToUpper(forms[j].GetName()) == upper)
            {
                return j;
            }
        }
        return -1;
    }

    public void AddChild(ISpaghettiScopeNode node)
    {
        this.children.Add((SpaghettiScopeNode)node);
    }

    public List<SpaghettiScopeNode> GetChildren()
    {
        return this.children;
    }

    public SpaghettiScopeNode? GetFirstChild()
    {
        return this.children.ElementAtOrDefault(0);
    }

    public IScopeIdentifier GetIdentifier()
    {
        return this.identifier;
    }

    public (Position start, Position end) CalcCoverage()
    {
        if (this.identifier.End == null)
        {
            throw new AssertError("internal error, caclCoverage");
        }
        return (this.identifier.Start, this.identifier.End);
    }

    public void SetEnd(Position end)
    {
        this.identifier.End = end;
    }

    public (global::ScipSharp.Indexer.Abap.FileInformation.Identifier? id, string ooType)? FindDeferred(string name)
    {
        SpaghettiScopeNode? search = this;

        while (search != null)
        {
            var found = search.GetData().Deferred.GetValueOrDefault(JsUtils.ToUpper(name));
            if (found != null)
            {
                return (new global::ScipSharp.Indexer.Abap.FileInformation.Identifier(found.Token, search.identifier.Filename), found.OoType);
            }
            search = search.GetParent();
        }

        return null;
    }

    public IClassDefinition? FindClassDefinition(string name)
    {
        SpaghettiScopeNode? search = this;

        var upper = JsUtils.ToUpper(name);
        while (search != null)
        {
            var c = search.GetData().Cdefs.GetValueOrDefault(upper);
            if (c != null)
            {
                return c;
            }
            search = search.GetParent();
        }

        return null;
    }

    public List<IClassDefinition> ListClassDefinitions()
    {
        SpaghettiScopeNode? search = this;
        List<IClassDefinition> ret = new List<IClassDefinition>();

        while (search != null)
        {
            ret.AddRange(search.GetData().Cdefs.Values.ToList());
            search = search.GetParent();
        }

        return ret;
    }

    public List<IInterfaceDefinition> ListInterfaceDefinitions()
    {
        SpaghettiScopeNode? search = this;
        List<IInterfaceDefinition> ret = new List<IInterfaceDefinition>();

        while (search != null)
        {
            ret.AddRange(search.GetData().Idefs.Values.ToList());
            search = search.GetParent();
        }

        return ret;
    }

    public IFormDefinition? FindFormDefinition(string name)
    {
        SpaghettiScopeNode? search = this;

        var upper = JsUtils.ToUpper(name);
        while (search != null)
        {
            var i = search.IndexOfForm(upper);
            if (i >= 0)
            {
                return search.GetData().Forms[i];
            }
            search = search.GetParent();
        }

        return null;
    }

    public IInterfaceDefinition? FindInterfaceDefinition(string name)
    {
        SpaghettiScopeNode? search = this;

        var upper = JsUtils.ToUpper(name);
        while (search != null)
        {
            var idef = search.GetData().Idefs.GetValueOrDefault(upper);
            if (idef != null)
            {
                return idef;
            }
            search = search.GetParent();
        }

        return null;
    }

    public TypedIdentifier? FindType(string name)
    {
        SpaghettiScopeNode? search = this;

        var upper = JsUtils.ToUpper(name);
        while (search != null)
        {
            var found = search.GetData().Types.GetValueOrDefault(upper);
            if (found != null)
            {
                return found;
            }
            search = search.GetParent();
        }

        return null;
    }

    public TypedIdentifier? FindExtraLikeType(string name)
    {
        SpaghettiScopeNode? search = this;

        var upper = JsUtils.ToUpper(name);
        while (search != null)
        {
            var data = search.GetData();
            if (data.ExtraLikeTypes.GetValueOrDefault(upper) != null)
            {
                return data.ExtraLikeTypes.GetValueOrDefault(upper);
            }
            search = search.GetParent();
        }

        return null;
    }

    public TypedIdentifier? FindVariable(string name)
    {
        SpaghettiScopeNode? search = this;

        var upper = JsUtils.ToUpper(name);
        while (search != null)
        {
            var data = search.GetData();
            if (data.Vars.GetValueOrDefault(upper) != null)
            {
                return data.Vars.GetValueOrDefault(upper);
            }
            search = search.GetParent();
        }

        return null;
    }

    public TypedIdentifier? FindWriteReference(Position pos)
    {
        foreach (var r in this.GetData().References)
        {
            if (r.ReferenceType == ReferenceType.DataWriteReference && r.Position.GetStart().Equals(pos))
            {
                if (r.Resolved is TypedIdentifier)
                {
                    return ((TypedIdentifier)r.Resolved);
                }
            }
        }

        return null;
    }

    public string? FindTableReference(Position pos)
    {
        foreach (var r in this.GetData().References)
        {
            if (r.ReferenceType == ReferenceType.TableReference && r.Position.GetStart().Equals(pos) && r.Resolved != null)
            {
                return r.Resolved.GetName();
            }
        }

        return null;
    }

    public bool FindTableVoidReference(Position pos)
    {
        foreach (var r in this.GetData().References)
        {
            if (r.ReferenceType == ReferenceType.TableVoidReference && r.Position.GetStart().Equals(pos))
            {
                return true;
            }
        }

        return false;
    }

    // this method is used in the transpiler
    public IScopeIdentifier? FindScopeForVariable(string name)
    {
        SpaghettiScopeNode? search = this;
        var upper = JsUtils.ToUpper(name);

        while (search != null)
        {
            if (search.GetData().Vars.GetValueOrDefault(upper) != null)
            {
                return search.GetIdentifier();
            }
            search = search.GetParent();
        }

        return null;
    }

    ISpaghettiScopeNode? ISpaghettiScopeNode.GetParent() => GetParent();
    List<ISpaghettiScopeNode> ISpaghettiScopeNode.GetChildren() => GetChildren().Cast<ISpaghettiScopeNode>().ToList();
    ISpaghettiScopeNode? ISpaghettiScopeNode.GetFirstChild() => GetFirstChild();
}

public class SpaghettiScope : ISpaghettiScope
{
    private readonly SpaghettiScopeNode node;

    public SpaghettiScope(SpaghettiScopeNode top)
    {
        this.node = top;
    }

    // list variable definitions across all nodes
    public List<IScopeVariable> ListDefinitions(string filename)
    {
        List<IScopeVariable> ret = new List<IScopeVariable>();

        foreach (var n in this.AllNodes())
        {
            if (n.GetIdentifier().Filename == filename)
            {
                var vars = n.GetData().Vars;
                foreach (var v in vars.Keys.ToList())
                {
                    if (vars.GetValueOrDefault(v).GetFilename() == filename)
                    {
                        ret.Add(new IScopeVariable { Name = v, Identifier = vars.GetValueOrDefault(v) });
                    }
                }
            }
        }

        return ret;
    }

    public List<global::ScipSharp.Indexer.Abap.FileInformation.Identifier> ListReadPositions(string filename)
    {
        List<global::ScipSharp.Indexer.Abap.FileInformation.Identifier> ret = new List<global::ScipSharp.Indexer.Abap.FileInformation.Identifier>();

        foreach (var n in this.AllNodes())
        {
            if (n.GetIdentifier().Filename == filename)
            {
                foreach (var v in n.GetData().References)
                {
                    if (v.ReferenceType == ReferenceType.DataReadReference && v.Position.GetFilename() == filename)
                    {
                        ret.Add(v.Position);
                    }
                }
            }
        }

        return ret;
    }

    public List<global::ScipSharp.Indexer.Abap.FileInformation.Identifier> ListWritePositions(string filename)
    {
        List<global::ScipSharp.Indexer.Abap.FileInformation.Identifier> ret = new List<global::ScipSharp.Indexer.Abap.FileInformation.Identifier>();

        foreach (var n in this.AllNodes())
        {
            if (n.GetIdentifier().Filename == filename)
            {
                foreach (var v in n.GetData().References)
                {
                    if (v.ReferenceType == ReferenceType.DataWriteReference && v.Position.GetFilename() == filename)
                    {
                        ret.Add(v.Position);
                    }
                }
            }
        }

        return ret;
    }

    public SpaghettiScopeNode? LookupPosition(Position? p, string? filename)
    {
        if (p == null || filename == null)
        {
            return null;
        }
        return this.LookupPositionTraverse(p, filename, this.node);
    }

    public ISpaghettiScopeNode? GetFirstChild()
    {
        return this.node.GetFirstChild();
    }

    public SpaghettiScopeNode GetTop()
    {
        return this.node;
    }

    /////////////////////////////
    private List<SpaghettiScopeNode> AllNodes()
    {
        List<SpaghettiScopeNode> ret = new List<SpaghettiScopeNode>();
        List<SpaghettiScopeNode> stack = new List<SpaghettiScopeNode> { this.node };

        while (stack.Count > 0)
        {
            var current = JsUtils.Pop(stack)!;
            ret.Add(current);
            stack.AddRange(current.GetChildren());
        }

        return ret;
    }

    private SpaghettiScopeNode? LookupPositionTraverse(Position p, string filename, SpaghettiScopeNode node)
    {
        var coverage = node.CalcCoverage();
        if (node.GetIdentifier().Filename == filename && p.IsBetween(coverage.start, coverage.end) == false)
        {
            return null;
        }

        // possible optimization: binary search the nodes
        foreach (var c in node.GetChildren())
        {
            var result = this.LookupPositionTraverse(p, filename, c);
            if (result != null)
            {
                return result;
            }
        }

        if (node.GetIdentifier().Filename == filename && p.IsBetween(coverage.start, coverage.end))
        {
            return node;
        }

        return null;
    }

    ISpaghettiScopeNode? ISpaghettiScope.LookupPosition(Position? p, string? filename) => LookupPosition(p, filename);
    ISpaghettiScopeNode ISpaghettiScope.GetTop() => GetTop();
}
