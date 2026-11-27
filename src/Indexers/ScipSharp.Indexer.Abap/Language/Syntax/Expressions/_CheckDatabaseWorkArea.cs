// Ported from abaplint packages/core/src/abap/5_syntax/expressions/_check_database_work_area.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

// byte length of a flat type in a unicode system, as a lower and an upper bound:
// IntegerType also stands for INT1 and INT2, and padding is left out of the lower bound
public static class CheckDatabaseWorkAreaHelper
{

    // byte length of a flat type in a unicode system, as a lower and an upper bound:
    // IntegerType also stands for INT1 and INT2, and padding is left out of the lower bound
    internal static Bounds? Bounds(AbstractType type)
    {
        if (type is CharacterType || type is NumericType)
        {
            return new Bounds { Min = TypeLength.Of(type) * 2, Max = TypeLength.Of(type) * 2, Align = 2 };
        }
        else if (type is DateType)
        {
            return new Bounds { Min = 16, Max = 16, Align = 2 };
        }
        else if (type is TimeType)
        {
            return new Bounds { Min = 12, Max = 12, Align = 2 };
        }
        else if (type is IntegerType)
        {
            return new Bounds { Min = 1, Max = 4, Align = 4 };
        }
        else if (type is Integer8Type || type is FloatType || type is DecFloat16Type || type is UTCLongType)
        {
            return new Bounds { Min = 8, Max = 8, Align = 8 };
        }
        else if (type is DecFloat34Type)
        {
            return new Bounds { Min = 16, Max = 16, Align = 16 };
        }
        else if (type is PackedType || type is HexType)
        {
            return new Bounds { Min = TypeLength.Of(type), Max = TypeLength.Of(type), Align = 1 };
        }
        else if (type is StructureType)
        {
            int min = 0;
            int max = 0;
            int align = 1;
            foreach (var component in ((StructureType)type).GetComponents())
            {
                var sub = Bounds(component.Type);
                if (sub == null)
                {
                    return null;
                }
                align = Math.Max(align, sub.Align);
                max = (int)Math.Ceiling((double)max / sub.Align) * sub.Align + sub.Max;
                min = min + sub.Min;
            }
            max = (int)Math.Ceiling((double)max / align) * align;
            return new Bounds { Min = min, Max = max, Align = align };
        }
        // deep, generic, void or unknown
        return null;
    }

    // INSERT/MODIFY/UPDATE dbtab FROM wa: the work area must be at least as long as the table line,
    // client field included, else the statement is a syntax error on the system
    public static void CheckDatabaseWorkArea(StatementNode node, IParseTypeObject? dbSource, Dictionary<ExpressionNode, AbstractType?> sourceTypes, SyntaxInput input)
    {
        if (!((dbSource is Table)))
        {
            return;
        }

        var children = node.GetChildren();
        ExpressionNode? workArea = null;
        for (var i = 0; i < children.Count - 1; i++)
        {
            var child = children.ElementAtOrDefault(i);
            if (child is TokenNode && JsUtils.ToUpper(((TokenNode)child).ConcatTokens()) == "FROM")
            {
                var next = children.ElementAtOrDefault(i + 1);
                if (next is ExpressionNode && (((ExpressionNode)next).Get()) is global::ScipSharp.Indexer.Abap.Statements.Expressions.SQLSource)
                {
                    workArea = ((ExpressionNode)next);
                }
                break;
            }
        }
        if (workArea == null)
        {
            return;
        }

        var source = workArea.FindFirstExpression<SimpleSource3>() ?? workArea.FindFirstExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>();
        if (source == null)
        {
            return;
        }
        var waType = sourceTypes.GetValueOrDefault(source);
        if (!((waType is StructureType)))
        {
            return;
        }

        var tableType = ((Table)dbSource).ParseType(input.Scope.GetRegistry());
        var waBounds = Bounds(((StructureType)waType));
        var tableBounds = Bounds(tableType);
        if (waBounds == null || tableBounds == null)
        {
            return;
        }

        if (waBounds.Max < tableBounds.Min)
        {
            var message = "The work area \"" + source.ConcatTokens() + "\" is not long enough for table " + ((Table)dbSource).GetName();
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, source.GetFirstToken(), message));
        }
    }
}

public class Bounds
{
    public int Min { get; set; }
    public int Max { get; set; }
    public int Align { get; set; }
}
