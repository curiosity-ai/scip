// Ported from abaplint packages/core/src/abap/2_statements/statements/data_end.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DataEnd : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var common = Seq("COMMON PART", OptPrio(Expr<DefinitionName>()));

        var structure = Seq("END OF",
                              AltPrio(common, Expr<DefinitionName>()));

        var valid = Seq("VALID BETWEEN", Expr<ComponentName>(), "AND", Expr<ComponentName>());

        return Seq("DATA", structure, OptPrio(valid));
    }
}
