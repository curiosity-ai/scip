// Ported from abaplint packages/core/src/abap/5_syntax/structures/types.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Structures;

public class Types
{
    public TypedIdentifier? RunSyntax(StructureNode node, SyntaxInput input, string? qualifiedNamePrefix = null)
    {
        var name = node.FindFirstExpression<NamespaceSimpleName>()!.GetFirstToken();
        List<IStructureComponent> components = new List<IStructureComponent>();
        AbstractType? voidd = null;

        if (qualifiedNamePrefix == null)
        {
            qualifiedNamePrefix = "";
        }

        foreach (var c in node.GetChildren())
        {
            var ctyp = c.Get();
            if (c is StatementNode)
            {
                if (ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Type)
                {
                    var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Type().RunSyntax(((StatementNode)c), input, qualifiedNamePrefix + name.GetStr() + "-");
                    if (found != null)
                    {
                        if (found.GetName().Length > 30)
                        {
                            input.Issues.Add(SyntaxInput.SyntaxIssue(input, found.GetToken(), "Structure field name longer than 30 characters"));
                        }
                        components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                    }
                }
                else if (ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.IncludeType)
                {
                    var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.IncludeType().RunSyntax(((StatementNode)c), input);
                    if (found is VoidType || found is UnknownType)
                    {
                        voidd = ((AbstractType)found);
                    }
                    else
                    {
                        components.AddRange(((List<IStructureComponent>)found));
                    }
                }
            }
            else if (c is StructureNode && ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.Types)
            {
                var found = new Types().RunSyntax(((StructureNode)c), input, qualifiedNamePrefix + name.GetStr() + "-");
                if (found != null)
                {
                    if (found.GetName().Length > 30)
                    {
                        input.Issues.Add(SyntaxInput.SyntaxIssue(input, found.GetToken(), "Structure field name longer than 30 characters"));
                    }
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                }
            }
        }

        if (voidd != null)
        {
            return new TypedIdentifier(name, input.Filename, voidd);
        }
        else if (components.Count == 0)
        {
            return null;
        }

        string qualifiedName = qualifiedNamePrefix + name.GetStr();
        if (input.Scope.GetType() == ScopeType.ClassDefinition || input.Scope.GetType() == ScopeType.Interface)
        {
            qualifiedName = input.Scope.GetName() + "=>" + qualifiedName;
        }

        return new TypedIdentifier(name, input.Filename, new global::ScipSharp.Indexer.Abap.Types.Basic.StructureType(components, qualifiedName));
    }
}
