// Ported from abaplint packages/core/src/abap/5_syntax/expressions/_check_text_symbol.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using ScipSharp.Indexer.Abap.Types.Basic;

namespace ScipSharp.Indexer.Abap.Syntax.Expressions;

// A text symbol, 'text'(001), is a character literal of type C. Unlike a plain
// literal it does not convert when passed to a method parameter typed STRING,
// the system reports it as not type-compatible. Returns true if an issue was reported.
public static class CheckTextSymbolHelper
{

    // A text symbol, 'text'(001), is a character literal of type C. Unlike a plain
    // literal it does not convert when passed to a method parameter typed STRING,
    // the system reports it as not type-compatible. Returns true if an issue was reported.
    public static bool CheckTextSymbol(ExpressionNode? source, AbstractType? parameterType, SyntaxInput input)
    {

        if (source == null || !((parameterType is StringType)))
        {
            return false;
        }

        var children = source.GetChildren();
        ExpressionNode? constant = children.Count == 1 ? source.FindDirectExpression<global::ScipSharp.Indexer.Abap.Statements.Expressions.Constant>() : (ExpressionNode?)null;
        if (constant?.FindDirectExpression<TextElementString>() == null)
        {
            return false;
        }

        var message = $"Text symbol \"{source.ConcatTokens()}\" is a character literal, not type-compatible with a STRING parameter";
        input.Issues.Add(SyntaxInput.SyntaxIssue(input, source.GetFirstToken(), message));
        return true;
    }
}
