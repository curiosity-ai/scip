// Ported from abaplint packages/core/src/abap/2_statements/statements/move_corresponding.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MoveCorresponding : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var keeping = Ver(Release.v740sp05, "KEEPING TARGET LINES", also: AlsoIn.OpenABAP);
        var expanding = Ver(Release.v740sp05, "EXPANDING NESTED TABLES", also: AlsoIn.OpenABAP);

        var move = Seq("MOVE-CORRESPONDING",
                         OptPrio("EXACT"),
                         Expr<Source>(),
                         "TO",
                         Expr<SimpleTarget>(),
                         OptPrio(expanding),
                         OptPrio(keeping));

        return move;
    }
}
