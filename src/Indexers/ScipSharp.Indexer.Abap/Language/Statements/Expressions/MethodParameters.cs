// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_parameters.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodParameters : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var exporting = Seq("EXPORTING", Expr<ParameterListS>());
        var importing = Seq("IMPORTING", Expr<ParameterListT>());
        var changing = Seq("CHANGING", Expr<ParameterListT>());
        var receiving = Seq("RECEIVING", Expr<ParameterT>());
        var exceptions = Seq("EXCEPTIONS", Expr<ParameterListExceptions>());
        var @long = Seq(OptPrio(exporting),
                         OptPrio(importing),
                         OptPrio(changing),
                         OptPrio(receiving),
                         OptPrio(exceptions));

        return @long;
    }
}
