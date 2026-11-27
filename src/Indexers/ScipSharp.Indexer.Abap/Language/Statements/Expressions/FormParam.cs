// Ported from abaplint packages/core/src/abap/2_statements/expressions/form_param.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FormParam : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var stru = Seq("STRUCTURE", Expr<SimpleFieldChain>());

        var ret = Seq(AltPrio(Expr<PassByValue>(), Expr<FormParamName>()),
                        OptPrio(AltPrio(Expr<FormParamType>(), stru)));

        return ret;
    }
}
