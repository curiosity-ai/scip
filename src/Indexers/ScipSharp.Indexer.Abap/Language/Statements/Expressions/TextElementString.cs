// Ported from abaplint packages/core/src/abap/2_statements/expressions/text_element_string.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TextElementString : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var text = Seq(Tok<Tokens.ParenLeft>(), Expr<TextElementKey>(), Tok<Tokens.ParenRightW>());
        var stri = Seq(Regex(@"^('.*')|(`.*`)$"), text);
        return stri;
    }
}
