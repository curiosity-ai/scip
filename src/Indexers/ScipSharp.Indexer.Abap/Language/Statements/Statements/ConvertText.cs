// Ported from abaplint packages/core/src/abap/2_statements/statements/convert_text.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ConvertText : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        return Seq("CONVERT TEXT",
                   Expr<Source>(),
                   "INTO SORTABLE CODE",
                   Expr<Target>());
    }
}
