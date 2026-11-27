// Ported from abaplint packages/core/src/abap/2_statements/statements/provide.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Provide : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var list = PlusPrio(AltPrio("*", Expr<ProvideFieldName>()));

        var fields = Seq("FIELDS",
                           list,
                           "FROM",
                           Expr<Source>(),
                           "INTO",
                           Expr<Target>(),
                           "VALID",
                           Expr<Expressions.Field>(),
                           "BOUNDS",
                           Expr<Expressions.Field>(),
                           "AND",
                           Expr<Expressions.Field>());

        var from = Seq("FROM", Expr<Source>());

        var fieldList = Seq(Plus(list), from);

        var where = Seq("WHERE", Expr<Cond>());
        var between = Seq("BETWEEN", Expr<SimpleSource3>(), "AND", Expr<SimpleSource3>());

        var ret = Seq("PROVIDE",
                        AltPrio(PlusPrio(fields), PlusPrio(fieldList)),
                        Opt(Per(between, where)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
