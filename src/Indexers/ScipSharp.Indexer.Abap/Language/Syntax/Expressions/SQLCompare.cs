// Ported from abaplint packages/core/src/abap/5_syntax/expressions/sql_compare.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

public class SQLCompare
{

    public static void RunSyntax(INode node, SyntaxInput input, List<IParseTypeObject?> tables)
    {

        AbstractType? sourceType = null;
        AbstractToken? token = null;

        if ((node.GetFirstChild()?.Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.Dynamic)
        {
            Dynamic.RunSyntax(((ExpressionNode)node.GetFirstChild()!), input);
            return;
        }

        foreach (var s in node.FindDirectExpressions<SimpleSource3>())
        {
            Source.RunSyntax(s, input);
        }

        foreach (var s in node.FindDirectExpressions<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSource>())
        {
            foreach (var child in s.GetChildren())
            {
                if (child is ExpressionNode)
                {
                    token = ((ExpressionNode)child).GetFirstToken();
                    break;
                }
            }

            sourceType = SQLSource.RunSyntax(s, input);
        }

        var sqlin = node.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLIn>();
        if (sqlin != null)
        {
            SQLIn.RunSyntax(sqlin, input);
        }

        var fieldName = JsUtils.ToUpper(node.FindDirectExpression<SQLFieldName>()?.ConcatTokens());
        if (!string.IsNullOrEmpty(fieldName) && sourceType != null && token != null)
        {
            // check compatibility for rule sql_value_conversion
            var targetType = SQLCompare.FindType(fieldName, tables, input.Scope);

            string message = "";
            if (sourceType is IntegerType && targetType is CharacterType)
            {
                message = $"{fieldName}: Integer to CHAR conversion";
            }
            else if (sourceType is IntegerType && targetType is NumericType)
            {
                message = $"{fieldName}: Integer to NUMC conversion";
            }
            else if (sourceType is NumericType && targetType is IntegerType)
            {
                message = $"{fieldName}: NUMC to Integer conversion";
            }
            else if (sourceType is CharacterType && targetType is IntegerType)
            {
                message = $"{fieldName}: CHAR to Integer conversion";
            }
            else if (sourceType is CharacterType && targetType is CharacterType && ((CharacterType)sourceType).GetLength() > ((CharacterType)targetType).GetLength())
            {
                message = $"{fieldName}: Source field longer than database field, CHAR -> CHAR";
            }
            else if (sourceType is NumericType && targetType is NumericType && ((NumericType)sourceType).GetLength() > ((NumericType)targetType).GetLength())
            {
                message = $"{fieldName}: Source field longer than database field, NUMC -> NUMC";
            }
            if (message != "")
            {
                input.Scope.AddSQLConversion(fieldName, message, token);
            }
        }
    }

    private static AbstractType? FindType(string fieldName, List<IParseTypeObject?> tables, CurrentScope scope)
    {
        foreach (var t in tables)
        {
            var type = t?.ParseType(scope.GetRegistry());
            if (type is StructureType)
            {
                return ((StructureType)type).GetComponentByName(fieldName);
            }
        }
        return null;
    }
}
