// Ported from abaplint packages/core/src/abap/2_statements/expressions/data_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DataDefinition : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var simple = Opt(Per("READ-ONLY",
                               Expr<Type>(),
                               Expr<Length>(),
                               Expr<Decimals>(),
                               Expr<Value>()));

        var table = Seq(Expr<TypeTable>(), OptPrio("READ-ONLY"));

        var boxed = Ver(Release.v702, "BOXED");

        return Seq(Expr<DefinitionName>(),
                   OptPrio(Expr<ConstantFieldLength>()),
                   AltPrio(Ver(Release.v731, LobHandleTypes.LobHandleType()), Alt(simple, table, Expr<TypeStructure>())),
                   OptPrio(boxed));
    }
}
