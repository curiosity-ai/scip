// Ported from abaplint packages/core/src/abap/2_statements/expressions/function_parameters.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FunctionParameters : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var exporting = Seq("EXPORTING", Expr<FunctionExporting>());
        var importing = Seq("IMPORTING", Expr<ParameterListT>());
        var changing = Seq("CHANGING", Expr<ParameterListT>());
        var tables = Seq("TABLES", Expr<ParameterListT>());
        var exceptions = Seq("EXCEPTIONS", Expr<ParameterListExceptions>());
        var @long = Seq(OptPrio(exporting),
                         OptPrio(importing),
                         OptPrio(tables),
                         OptPrio(changing),
                         OptPrio(exceptions));

        return @long;
    }
}
