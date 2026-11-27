// Ported from abaplint packages/core/src/abap/2_statements/statements/assign.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Assign : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var dynamicType = AltPrio(VerNotLang(LanguageVersion.KeyUser, Expr<Dynamic>()), Expr<TypeName>());
        var type = Seq("TYPE", dynamicType);
        var like = Seq("LIKE", AltPrio(Expr<Dynamic>(), Expr<Source>()));
        var handle = Seq("TYPE HANDLE", Expr<Source>());
        var range = Seq("RANGE", Expr<Source>());
        var decimals = Seq("DECIMALS", Expr<Source>());

        var castingType = Seq("CASTING", Opt(Alt(like, handle, Per(type, decimals))));
        // CASTING TYPE (dyntype) blocked in KeyUser
        var casting = VerNotLang(LanguageVersion.KeyUser, castingType);
        var obsoleteType = Seq("TYPE", Expr<Source>(), OptPrio(decimals));

        var ret = Seq("ASSIGN",
                        Opt(VerNotLang(LanguageVersion.KeyUser, Seq(Expr<Target>(), "INCREMENT"))),
                        Expr<AssignSource>(),
                        "TO",
                        Expr<FSTarget>(),
                        Opt(AltPrio(casting, obsoleteType, decimals)),
                        Opt(range),
                        Opt(Ver(Release.v757, "ELSE UNASSIGN")));

        return ret;
    }
}
