// Ported from abaplint packages/core/src/abap/2_statements/statements/data_begin.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DataBegin : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var occurs = Seq("OCCURS", AltPrio(Expr<Integer>(), Expr<FieldChain>()));

        var common = Seq("COMMON PART", OptPrio(Expr<DefinitionName>()));

        var structure = Seq("BEGIN OF",
                              AltPrio(common, Seq(
                                Expr<DefinitionName>(),
                                OptPrio("READ-ONLY"),
                                OptPrio(occurs))));

        return Seq("DATA", structure);
    }
}
