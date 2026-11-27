// Ported from abaplint packages/core/src/abap/2_statements/expressions/field_sub.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FieldSub : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var ret = Seq(Regex(@"^\*?!?&?(\/\w+\/)?[a-zA-Z_%$\?][\w%$\$\*]*$"),
                        StarPrio(Seq(Tok<Tokens.Dash>(), Regex(@"^[\w%$\$\*]+$"))),
                        Opt(Expr<FieldLength>()),
                        Opt(Expr<TableBody>()));

        return ret;
    }
}
