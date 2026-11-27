// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_target.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLTarget : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var n = Ver(Release.v754, "NEW");
        var at = Ver(Release.v740sp05, Seq(Opt(n), AltPrio(Tok<Tokens.WAt>(), Tok<Tokens.At>()), Expr<Target>()), also: AlsoIn.OpenABAP);

        return AltPrio(at, Expr<Target>());
    }
}
