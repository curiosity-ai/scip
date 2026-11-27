// Ported from abaplint packages/core/src/abap/2_statements/expressions/value_body_line.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ValueBodyLine : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        // missing spaces caught by rule "parser_missing_space"
        var ret = Seq(AltPrio(Tok<Tokens.WParenLeftW>(), Tok<Tokens.WParenLeft>()),
                        OptPrio(AltPrio(PlusPrio(Expr<FieldAssignment>()), Expr<ValueBodyLines>(), Expr<Source>())),
                        AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.ParenRightW>()));

        return ret;
    }
}
