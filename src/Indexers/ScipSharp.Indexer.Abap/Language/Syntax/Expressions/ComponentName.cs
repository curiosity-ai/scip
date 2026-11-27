// Ported from abaplint packages/core/src/abap/5_syntax/expressions/component_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class ComponentName
{
    public static AbstractType? RunSyntax(AbstractType? context, INode node, SyntaxInput input)
    {
        if (context is VoidType)
        {
            return ((VoidType)context);
        }

        var nameToken = node.GetFirstToken();
        var name = nameToken.GetStr();

        if (context is StructureType)
        {
            var ret = ((StructureType)context).GetComponentByName(name);
            if (ret == null)
            {
                input.Issues.Add(SyntaxInput.SyntaxIssue(input, nameToken, "Component \"" + name + "\" not found in structure"));
                return VoidType.Get(SyntaxInput.CheckSyntaxKey);
            }
            return ret;
        }

        if (context is TableType && ((TableType)context).IsWithHeader() == true)
        {
            var rowType = ((TableType)context).GetRowType();
            if (rowType is VoidType)
            {
                return ((TableType)context);
            }
            else if (JsUtils.ToUpper(name) == "TABLE_LINE")
            {
                return rowType;
            }
            else if (rowType is StructureType)
            {
                var ret = ((StructureType)rowType).GetComponentByName(name);
                if (ret == null)
                {
                    input.Issues.Add(SyntaxInput.SyntaxIssue(input, nameToken, "Component \"" + name + "\" not found in structure"));
                    return VoidType.Get(SyntaxInput.CheckSyntaxKey);
                }
                return ret;
            }
        }

        if (!((context is UnknownType)))
        {
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, nameToken, "Not a structure, ComponentName, \"" + name + "\", (" + JsUtils.Str(context?.GetType().Name) + ")"));
        }

        return VoidType.Get(SyntaxInput.CheckSyntaxKey);
    }
}
