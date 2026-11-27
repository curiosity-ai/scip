// Ported from abaplint packages/core/src/abap/2_statements/statements/read_table.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ReadTable : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var comparing = Seq("COMPARING", Alt(Plus(Expr<FieldSub>()), Plus(Expr<Dynamic>())));

        var index = Seq("INDEX", Expr<Source>());

        // Dynamic key name or dynamic component name blocked in KeyUser
        var components = Seq(VerNotLang(LanguageVersion.KeyUser, Alt(Expr<Expressions.Field>(), Expr<Dynamic>())), "COMPONENTS", Expr<ComponentCompareSimple>());

        var key = Seq(AltPrio("WITH KEY", "WITH TABLE KEY"),
                        Alt(Expr<ComponentCompareSimple>(),
                            components,
                            Seq(OptPrio("="), Expr<Source>())));

        var @using = Seq("USING KEY", Alt(Expr<Expressions.Field>(), VerNotLang(LanguageVersion.KeyUser, Expr<Dynamic>())));

        var from = Seq("FROM", Expr<Source>());

        var transporting = Seq("TRANSPORTING", AltPrio("ALL FIELDS", "NO FIELDS", Expr<TransportingFields>()));
        object[] common = [Expr<ReadTableTarget>(), comparing, "CASTING", transporting, "BINARY SEARCH"];

        var perm = Alt(PerOf([Alt(index, from), @using, .. common]),
                         PerOf([key, .. common]),
                         PerOf([.. common]));

        return Seq("READ TABLE",
                   Alt(Expr<SimpleSource2>(), Ver(Release.v740sp02, Expr<Source>(), also: AlsoIn.OpenABAP)),
                   Opt(perm));
    }
}
