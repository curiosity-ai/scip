// Ported from abaplint packages/core/src/abap/5_syntax/structures/data.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Types;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Structures;

public class Data
{
    public static TypedIdentifier? RunSyntax(StructureNode node, SyntaxInput input)
    {
        var fouth = node.GetFirstChild()?.GetChildren().ElementAtOrDefault(3);
        var isCommonPart = fouth is TokenNode && JsUtils.ToUpper(((TokenNode)fouth).ConcatTokens()) == "COMMON";
        if (isCommonPart)
        {
            Data.RunCommonPartSyntax(node, input);
            return null;
        }

        var nameExpr = node.FindFirstExpression<DefinitionName>()!;
        AbstractToken name = nameExpr.GetFirstToken();
        if (nameExpr.CountTokens() > 1)
        {
            name = new Identifier(name.GetStart(), nameExpr.ConcatTokens());
        }
        bool table = false;
        Dictionary<string, object?> values = new Dictionary<string, object?>();

        List<IStructureComponent> components = new List<IStructureComponent>();
        foreach (var c in node.GetChildren())
        {
            var ctyp = c.Get();
            if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Data)
            {
                var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Data().RunSyntax(((StatementNode)c), input);
                if (found != null)
                {
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                    if (found.GetValue() != null)
                    {
                        values[found.GetName()] = found.GetValue();
                    }
                }
            }
            else if (c is StructureNode && ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.Data)
            {
                var found = Data.RunSyntax(((StructureNode)c), input);
                if (found != null)
                {
                    components.Add(new IStructureComponent { Name = found.GetName(), Type = found.GetType() });
                }
            }
            else if (c is StatementNode && ctyp is DataBegin)
            {
                if ((((StatementNode)c).FindDirectTokenByText("OCCURS")) != null)
                {
                    table = true;
                }
            }
            else if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.IncludeType)
            {
                // INCLUDES
                var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.IncludeType().RunSyntax(((StatementNode)c), input);
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
                else if (found is UnknownType)
                {
                    return new TypedIdentifier(name, input.Filename, ((UnknownType)found));
                }

                foreach (var c2 in ((List<IStructureComponent>)found))
                {
                    components.Add(c2);
                }
            }
        }

        if (table == true)
        {
            return new TypedIdentifier(name, input.Filename, new TableType(new global::ScipSharp.Indexer.Abap.Types.Basic.StructureType(components), new ITableOptions { WithHeader = true, KeyType = TableKeyType.@default }));
        }
        else
        {
            Dictionary<string, object?>? val = values.Keys.ToList().Count > 0 ? values : null;
            return new TypedIdentifier(name, input.Filename, new global::ScipSharp.Indexer.Abap.Types.Basic.StructureType(components), null, val);
        }
    }

    private static void RunCommonPartSyntax(StructureNode node, SyntaxInput input)
    {
        foreach (var c in node.GetChildren())
        {
            var ctyp = c.Get();
            if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Data)
            {
                var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Data().RunSyntax(((StatementNode)c), input);
                if (found != null)
                {
                    input.Scope.AddIdentifier(found);
                }
            }
            else if (c is StructureNode && ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.Data)
            {
                var found = Data.RunSyntax(((StructureNode)c), input);
                if (found != null)
                {
                    input.Scope.AddIdentifier(found);
                }
            }
            else if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Type)
            {
                var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Type().RunSyntax(((StatementNode)c), input);
                if (found != null)
                {
                    input.Scope.AddType(found);
                }
            }
            else if (c is StructureNode && ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.Types)
            {
                var found = new Types().RunSyntax(((StructureNode)c), input);
                if (found != null)
                {
                    input.Scope.AddType(found);
                }
            }
            else if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Constant)
            {
                var found = new global::ScipSharp.Indexer.Abap.Syntax.Statements.Constant().RunSyntax(((StatementNode)c), input);
                if (found != null)
                {
                    input.Scope.AddIdentifier(found);
                }
            }
            else if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Ranges)
            {
                new global::ScipSharp.Indexer.Abap.Syntax.Statements.Ranges().RunSyntax(((StatementNode)c), input);
            }
            else if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.SelectOption)
            {
                new global::ScipSharp.Indexer.Abap.Syntax.Statements.SelectOption().RunSyntax(((StatementNode)c), input);
            }
            else if (c is StatementNode && ctyp is global::ScipSharp.Indexer.Abap.Statements.Statements.Parameter)
            {
                new global::ScipSharp.Indexer.Abap.Syntax.Statements.Parameter().RunSyntax(((StatementNode)c), input);
            }
            else if (c is StructureNode && ctyp is global::ScipSharp.Indexer.Abap.Structures.Structures.Constants)
            {
                var __d0 = new Constants().RunSyntax(((StructureNode)c), input);
                var found = __d0.type;
                var _ = __d0.values;
                if (found != null)
                {
                    input.Scope.AddIdentifier(found);
                }
            }
        }
    }
}
