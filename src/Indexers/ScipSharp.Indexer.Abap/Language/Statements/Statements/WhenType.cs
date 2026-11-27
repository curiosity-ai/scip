// Ported from abaplint packages/core/src/abap/2_statements/statements/when_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class WhenType : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var into = Seq("INTO", Expr<Target>());

        var type = Seq(Expr<ClassName>(), OptPrio(into));

        return Ver(Release.v750, Seq("WHEN TYPE", type), also: AlsoIn.OpenABAP);
    }
}
