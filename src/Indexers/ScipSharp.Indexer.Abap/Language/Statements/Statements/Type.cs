// Ported from abaplint packages/core/src/abap/2_statements/statements/type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Type : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var simple = Per(Expr<Expressions.Type>(), Expr<Decimals>(), Expr<Length>());

        var clientSpecified = Ver(Release.v760, Seq("TYPE", Expr<TypeName>(), "CLIENT", "SPECIFIED",
                                                      Expr<FieldChain>(),
                                                      Opt(Plus(Seq(LobHandleTypes.LobKind(), LobHandleTypes.LobColumns())))));

        var anyStructure = Ver(Release.v916, Seq("TYPE", "ANY", "STRUCTURE",
                                                   Opt(Seq("CONTAINING",
                                                           Plus(Seq(Expr<DefinitionName>(), "TYPE", Expr<TypeSimpleRef>()))))));

        var def = Seq(Expr<NamespaceSimpleName>(),
                        Opt(Expr<ConstantFieldLength>()),
                        Opt(AltPrio(Ver(Release.v731, LobHandleTypes.LobHandleType()), clientSpecified, anyStructure,
                                    Alt(simple, Expr<TypeTable>(), Expr<TypeStructure>()))));

        // todo, BOXED is only allowed with structures inside structures?
        var boxed = Ver(Release.v702, "BOXED");

        var ret = Seq("TYPES", def, Opt(boxed));

        return ret;
    }
}
