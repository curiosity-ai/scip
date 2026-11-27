// Ported from abaplint packages/core/src/abap/2_statements/expressions/ole_exporting.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class OLEExporting : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var fields = Seq(Regex(@"^[&_!#\*]?[\w\d\*%\$\?#]+$"), "=", Expr<Source>());
        return Seq("EXPORTING", Plus(fields));
    }
}
