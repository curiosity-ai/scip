// Ported from abaplint packages/core/src/abap/2_statements/expressions/receive_parameters.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ReceiveParameters : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var importing = Seq("IMPORTING", Expr<ParameterListT>());
        var tables = Seq("TABLES", Expr<ParameterListT>());
        var changing = Seq("CHANGING", Expr<ParameterListT>());
        var exceptions = Seq("EXCEPTIONS", Opt(Expr<ParameterListExceptions>()), Opt(Expr<Field>()));
        var @long = Seq(Opt(importing),
                         Opt(changing),
                         Opt(tables),
                         Opt(exceptions));

        return @long;
    }
}
