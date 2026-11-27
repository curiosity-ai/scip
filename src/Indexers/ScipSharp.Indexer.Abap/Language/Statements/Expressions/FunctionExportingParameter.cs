// Ported from abaplint packages/core/src/abap/2_statements/expressions/function_exporting_parameter.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FunctionExportingParameter : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var s = AltPrio(Ver(Release.v740sp02, Expr<Source>(), also: AlsoIn.OpenABAP), Expr<SimpleSource3>());
        var exp = Seq(Expr<ParameterName>(), "=", s);

        return exp;
    }
}
