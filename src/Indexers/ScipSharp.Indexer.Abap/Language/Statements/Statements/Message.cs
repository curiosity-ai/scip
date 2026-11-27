// Ported from abaplint packages/core/src/abap/2_statements/statements/message.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Message : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var like = Seq("DISPLAY LIKE", Expr<Source>());
        var into = Seq("INTO", Expr<Target>());
        // RAISING addition blocked in KeyUser
        var raising = VerNotLang(LanguageVersion.KeyUser, Seq("RAISING", Expr<ExceptionName>()));

        var options = Per(like, into, raising);

        var type = Seq("TYPE", Expr<Source>());

        var sou = AltPrio(options, Expr<MessageSourceSource>());
        var sourc = Alt(sou,
                          Seq(Expr<MessageSourceSource>(), sou),
                          Seq(Expr<MessageSourceSource>(), Expr<MessageSourceSource>(), sou),
                          Seq(Expr<MessageSourceSource>(), Expr<MessageSourceSource>(), Expr<MessageSourceSource>(), options));

        var mwith = Seq("WITH", Expr<MessageSourceSource>(), Opt(sourc));

        var foo = Seq(Expr<MessageSource>(), Opt(options), Opt(mwith));
        var text = Seq(Expr<MessageSourceSource>(), type, OptPrio(like), OptPrio(raising));

        var cloud1 = Seq(Opt(Seq("WITH", Expr<Source>(), Opt(Expr<Source>()), Opt(Expr<Source>()), Opt(Expr<Source>()))), AltPrio(into, raising));
        var cloud2 = Seq(AltPrio(into, raising), Opt(Seq("WITH", Expr<Source>(), Opt(Expr<Source>()), Opt(Expr<Source>()), Opt(Expr<Source>()))));
        var cloud = Seq(Expr<MessageSource>(), Alt(cloud1, cloud2));

        var ret = Seq("MESSAGE", AltPrio(
          VerNotLang(LanguageVersion.Cloud, foo),
          VerNotLang(LanguageVersion.Cloud, text),
          VerLang(LanguageVersion.Cloud, cloud)));

        return ret;
    }
}
