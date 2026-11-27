// Ported from abaplint packages/core/src/abap/5_syntax/structures/statics.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Structures;

// todo, this is much like DATA, refactor?
public class Statics
{
    public TypedIdentifier? RunSyntax(StructureNode node, SyntaxInput input)
    {
        var name = node.FindFirstExpression<DefinitionName>()!.GetFirstToken();
        bool table = false;

        List<IStructureComponent> components = new List<IStructureComponent>();
        foreach (var c in node.GetChildren())
        {
            var ctyp = c.Get();
            if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Static)
            {
                var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Static().RunSyntax(((StatementNode)c), input);
                if (found != null)
                {
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                }
            }
            else if (c is StructureNode && ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.Statics)
            {
                var found = new Statics().RunSyntax(((StructureNode)c), input);
                if (found != null)
                {
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                }
            }
            else if (c is StatementNode && ctyp is StaticBegin)
            {
                if ((((StatementNode)c).FindDirectTokenByText("OCCURS")) != null)
                {
                    table = true;
                }
            }
            else if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.IncludeType)
            {
                // INCLUDES
                var typeName = ((StatementNode)c).FindFirstExpression<TypeName>()?.GetFirstToken().GetStr();
                AbstractType? found = input.Scope.FindType(typeName)?.GetType();
                if (found == null)
                {
                    var f = input.Scope.GetDDIC().LookupTableOrView(typeName).Type;
                    if (((object)f) is TypedIdentifier)
                    {
                        found = ((TypedIdentifier)(object)f).GetType();
                    }
                    else
                    {
                        found = f;
                    }
                }
                if (found is VoidType)
                {
                    if (table == true)
                    {
                        var ttyp = new TableType(((VoidType)found), new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default });
                        return new TypedIdentifier(name, input.Filename, ttyp);
                    }
                    else
                    {
                        return new TypedIdentifier(name, input.Filename, ((VoidType)found));
                    }
                }
                if (found is UnknownType)
                {
                    return new TypedIdentifier(name, input.Filename, new UnknownType("unknown type, " + JsUtils.Str(typeName)));
                }
                if (!((found is global::ScipSharp.Indexer.Abap.Types.Basic.StructureType)))
                {
                    var message = "not structured, " + JsUtils.Str(typeName);
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, node.GetFirstToken(), message));
                    return new TypedIdentifier(name, input.Filename, VoidType.Get(SyntaxInput.CheckSyntaxKey));
                }
                foreach (var c2 in ((global::ScipSharp.Indexer.Abap.Types.Basic.StructureType)found).GetComponents())
                {
                    components.Add(c2);
                }
            }
        }

        if (table == true)
        {
            var ttyp = new TableType(new global::ScipSharp.Indexer.Abap.Types.Basic.StructureType(components), new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default });
            return new TypedIdentifier(name, input.Filename, ttyp);
        }
        else
        {
            return new TypedIdentifier(name, input.Filename, new global::ScipSharp.Indexer.Abap.Types.Basic.StructureType(components));
        }
    }
}
