// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_creating.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLCreating : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var typeSpecs = Seq(Expr<SQLCreatingTypeSpec>(), StarPrio(Expr<SQLCreatingTypeSpec>()));
        return Seq("CREATING", AltPrio(Expr<Dynamic>(), typeSpecs));
    }
}
