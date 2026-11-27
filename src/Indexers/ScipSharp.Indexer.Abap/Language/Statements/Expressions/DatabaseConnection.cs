// Ported from abaplint packages/core/src/abap/2_statements/expressions/database_connection.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DatabaseConnection : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var name = Regex(@"[\w\/]+");
        return Seq("CONNECTION", AltPrio(name, Expr<ConstantString>(), Expr<Dynamic>()));
    }
}
