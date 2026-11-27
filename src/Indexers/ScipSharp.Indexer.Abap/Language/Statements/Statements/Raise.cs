// Ported from abaplint packages/core/src/abap/2_statements/statements/raise.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Raise : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var mess = Seq("MESSAGE",
                         Expr<MessageSource>(),
                         Opt(Expr<RaiseWith>()));

        var messid = Seq("MESSAGE ID",
                           Expr<Source>(),
                           "NUMBER",
                           AltPrio(Expr<MessageNumber>(), Expr<Source>()),
                           OptPrio(Expr<RaiseWith>()));

        var exporting = Seq("EXPORTING", Expr<ParameterListS>());

        var from = Seq("TYPE",
                         Expr<ClassName>(),
                         Opt(Alt(Ver(Release.v750, Alt(mess, messid), also: AlsoIn.OpenABAP), Ver(Release.v752, "USING MESSAGE"))),
                         OptPrio(exporting));

        var pre = AltPrio(Seq(OptPrio("RESUMABLE"), "EXCEPTION"), "SHORTDUMP");

        var clas = Seq(pre,
                         AltPrio(from, Ver(Release.v752, Expr<Source>(), also: AlsoIn.OpenABAP), Expr<SimpleSource2>()));

        // Old form "RAISE exception_name" blocked in KeyUser
        var ret = Seq("RAISE", AltPrio(clas, VerNotLang(LanguageVersion.KeyUser, Expr<ExceptionName>())));

        return ret;
    }
}
