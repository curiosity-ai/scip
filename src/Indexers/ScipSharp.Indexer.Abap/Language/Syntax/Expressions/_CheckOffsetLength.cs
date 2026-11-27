// Ported from abaplint packages/core/src/abap/5_syntax/expressions/_check_offset_length.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

// Offsets or lengths cannot be specified for STRING or XSTRING fields
// passed as a method parameter source. Returns true if an issue was reported.
public static class CheckOffsetLengthHelper
{

    // Offsets or lengths cannot be specified for STRING or XSTRING fields
    // passed as a method parameter source. Returns true if an issue was reported.
    public static bool CheckOffsetLength(ExpressionNode source, AbstractType? sourceType, object method, SyntaxInput input)
    {

        var chain = source.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldChain>();
        var isCalculated = source.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Source>() != null;
        var hasOffsetOrLength = chain != null && isCalculated == false && (chain.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldOffset>() != null || chain.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.FieldLength>() != null);
        if (hasOffsetOrLength && !((method is BuiltInMethod)) && (sourceType is StringType || sourceType is XStringType))
        {
            var message = "Offsets or lengths cannot be specified for fields of type \"STRING\" or \"XSTRING\" in the current statement";
            input.Issues.Add(SyntaxInput.SyntaxIssue(input, source.GetFirstToken(), message));
            return true;
        }

        return false;
    }
}
