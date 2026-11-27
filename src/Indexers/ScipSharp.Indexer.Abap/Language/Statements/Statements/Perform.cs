// Ported from abaplint packages/core/src/abap/2_statements/statements/perform.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Perform : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var level = Seq("LEVEL", Expr<Source>());
        var commit = Alt(Seq("ON COMMIT", Opt(level)),
                           "ON ROLLBACK");

        var @short = VerNotLang(LanguageVersion.Cloud, Seq(Expr<FormName>(),
                                                            Tok<Tokens.ParenLeft>(),
                                                            Expr<IncludeName>(),
                                                            Tok<Tokens.ParenRightW>()));

        var program = Seq("IN PROGRAM", Opt(Alt(Expr<Dynamic>(), Expr<IncludeName>())));

        var found = Str("IF FOUND");

        var full = Seq(Alt(Expr<FormName>(), Expr<Dynamic>()),
                         Opt(VerNotLang(LanguageVersion.Cloud, program)));

        var normal = Seq(Opt(found),
                           Opt(Expr<PerformTables>()),
                           Opt(Expr<PerformUsing>()),
                           Opt(Expr<PerformChanging>()),
                           Opt(found),
                           Opt(commit));

        var of = Seq("OF", Plus(Expr<FormName>()));

        var ret = Seq("PERFORM",
                        Alt(@short, full),
                        AltPrio(of, normal));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
