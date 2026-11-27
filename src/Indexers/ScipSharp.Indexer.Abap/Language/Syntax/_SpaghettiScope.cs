// Ported from abaplint packages/core/src/abap/5_syntax/_spaghetti_scope.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Types;

namespace ScipSharp.Indexer.Abap.Syntax;

public class IScopeIdentifier
{
    public string Stype { get; set; }
    public string Sname { get; set; }
    public string Filename { get; set; }
    public Position Start { get; set; }
    public Position? End { get; set; }
}

public class IScopeVariable
{
    public string Name { get; set; }
    public TypedIdentifier Identifier { get; set; }
}

public class DeferredInformation
{
    public AbstractToken Token { get; set; }
    public string OoType { get; set; }
}

public class IScopeData
{
    public Dictionary<string, TypedIdentifier> Vars { get; set; }
    public Dictionary<string, TypedIdentifier> Types { get; set; }
    // static methods can type LIKE instance variables that are not visible
    public Dictionary<string, TypedIdentifier> ExtraLikeTypes { get; set; }

    public Dictionary<string, DeferredInformation> Deferred { get; set; }

    public Dictionary<string, IClassDefinition> Cdefs { get; set; }
    public Dictionary<string, IInterfaceDefinition> Idefs { get; set; }
    public List<IFormDefinition> Forms { get; set; }

    public List<IReference> References { get; set; }
    public List<(string fieldName, string message, AbstractToken token)> SqlConversion { get; set; }
}

public interface ISpaghettiScopeNode
{
    ISpaghettiScopeNode? GetParent();
    void AddChild(ISpaghettiScopeNode node);
    List<ISpaghettiScopeNode> GetChildren();
    ISpaghettiScopeNode? GetFirstChild();
    IScopeIdentifier GetIdentifier();
    IScopeData GetData();
    (Position start, Position end) CalcCoverage();
    IClassDefinition? FindClassDefinition(string name);
    List<IClassDefinition> ListClassDefinitions();
    List<IInterfaceDefinition> ListInterfaceDefinitions();
    IFormDefinition? FindFormDefinition(string name);
    IInterfaceDefinition? FindInterfaceDefinition(string name);
    TypedIdentifier? FindType(string name);
    TypedIdentifier? FindVariable(string name);
    IScopeIdentifier? FindScopeForVariable(string name);
    TypedIdentifier? FindWriteReference(Position pos);
    string? FindTableReference(Position pos);
    bool FindTableVoidReference(Position pos);
}

public interface ISpaghettiScope
{
    List<IScopeVariable> ListDefinitions(string filename);
    List<global::ScipSharp.Indexer.Abap.FileInformation.Identifier> ListReadPositions(string filename);
    List<global::ScipSharp.Indexer.Abap.FileInformation.Identifier> ListWritePositions(string filename);
    ISpaghettiScopeNode? LookupPosition(Position? p, string? filename);
    ISpaghettiScopeNode GetTop();
    ISpaghettiScopeNode? GetFirstChild();
}

public class ISyntaxResult
{
    public List<Issue> Issues { get; init; }
    public ISpaghettiScope Spaghetti { get; init; }
}
