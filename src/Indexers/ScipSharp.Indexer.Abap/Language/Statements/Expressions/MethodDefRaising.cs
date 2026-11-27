// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_def_raising.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodDefRaising : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var resumable = Seq("RESUMABLE",
                              Tok<Tokens.ParenLeft>(),
                              Expr<ClassName>(),
                              Tok<Tokens.ParenRightW>());

        // parameter section keywords cannot be exception names
        var name = Seq(StopBefore1("IMPORTING", "EXPORTING", "CHANGING", "RETURNING", "RAISING", "EXCEPTIONS"), Expr<ClassName>());

        var raising = Seq("RAISING", Plus(AltPrio(resumable, name)));

        return raising;
    }
}
