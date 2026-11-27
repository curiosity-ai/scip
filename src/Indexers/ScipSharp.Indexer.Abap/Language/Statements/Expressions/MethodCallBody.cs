// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_call_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodCallBody : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var dynamicPar = Seq("PARAMETER-TABLE", Expr<Source>());
        var dynamicExc = Seq("EXCEPTION-TABLE", Expr<Source>());
        var dynamic = Seq(dynamicPar, OptPrio(dynamicExc));

        return Alt(Expr<MethodCallParam>(), Expr<MethodParameters>(), dynamic);
    }
}
