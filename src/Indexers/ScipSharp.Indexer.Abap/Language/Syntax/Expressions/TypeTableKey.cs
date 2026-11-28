// Ported from abaplint packages/core/src/abap/5_syntax/expressions/type_table_key.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class TypeTableKey
{
    public static UnknownType? RunSyntax(ExpressionNode node, AbstractType type)
    {
        if (type is TableType)
        {
            var rowType = ((TableType)type).GetRowType();
            if (rowType is StructureType)
            {
                foreach (var c in node.FindAllExpressions<FieldSub>())
                {
                    var concat = c.ConcatTokens() is var ct && ct.StartsWith('!') ? ct.Substring(1) : ct; // TS replace(/^!/, "")
                    if (concat.Contains("-", StringComparison.Ordinal) == false && ((StructureType)rowType).GetComponentByName(concat) == null && JsUtils.ToUpper(concat) != "TABLE_LINE")
                    {
                        return new UnknownType($"Field {concat} not part of structure");
                    }
                }
            }
        }
        return null;
    }
}
