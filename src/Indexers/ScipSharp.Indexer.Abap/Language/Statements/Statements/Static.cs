// Ported from abaplint packages/core/src/abap/2_statements/statements/static.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Static : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var p = Opt(Per(Expr<Expressions.Type>(), Expr<Value>(), Expr<Length>(), Expr<Decimals>()));

        var type = Seq(Opt(Expr<ConstantFieldLength>()), p);

        var body = Seq(Expr<DefinitionName>(),
                         OptPrio(Expr<ConstantFieldLength>()),
                         Alt(type, Expr<TypeTable>()));

        return AltPrio(
          Seq("STATIC", body),
          VerNotLang(LanguageVersion.KeyUser, Seq("STATICS", body)));
    }
}
