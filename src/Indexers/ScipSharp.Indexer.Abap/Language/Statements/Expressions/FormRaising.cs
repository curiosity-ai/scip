// Ported from abaplint packages/core/src/abap/2_statements/expressions/form_raising.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FormRaising : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var resume = Seq("RESUMABLE",
                           Tok<Tokens.ParenLeft>(),
                           Expr<ClassName>(),
                           Tok<Tokens.ParenRightW>());

        var raising = Seq("RAISING", Plus(Alt(Expr<ClassName>(), resume)));

        return raising;
    }
}
