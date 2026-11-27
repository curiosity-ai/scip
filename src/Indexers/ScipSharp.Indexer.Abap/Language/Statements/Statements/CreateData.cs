// Ported from abaplint packages/core/src/abap/2_statements/statements/create_data.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

// todo, similar to DATA or TYPES?
public class CreateData : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var areaHandle = Seq("AREA HANDLE", Expr<Source>());
        var typeHandle = Seq("TYPE HANDLE", Expr<SimpleSource3>());

        var type = Seq(Alt("TYPE",
                             "TYPE REF TO",
                             "TYPE TABLE OF",
                             "TYPE TABLE OF REF TO",
                             "TYPE SORTED TABLE OF",
                             "TYPE HASHED TABLE OF",
                             "TYPE STANDARD TABLE OF",
                             "TYPE LINE OF"),
                         Alt(Expr<TypeName>(), Expr<Dynamic>()));

        var like = Seq(Alt("LIKE",
                             "LIKE HASHED TABLE OF",
                             "LIKE LINE OF",
                             "LIKE STANDARD TABLE OF",
                             "LIKE SORTED TABLE OF",
                             "LIKE TABLE OF"),
                         Alt(Expr<Source>(), Expr<Dynamic>()));

        var length = Seq("LENGTH", Expr<Source>());
        var initial = Seq("INITIAL SIZE", Expr<Source>());
        var decimals = Seq("DECIMALS", Expr<Source>());
        var uniq = Alt("UNIQUE", "NON-UNIQUE");
        var emptyKey = Ver(Release.v740sp02, "EMPTY KEY", also: AlsoIn.OpenABAP);
        var def = Seq(Opt(uniq), Alt("DEFAULT KEY", emptyKey));

        var kdef = Seq(Opt(uniq), "KEY", Alt(Plus(Expr<Expressions.Field>()), Expr<Dynamic>()));

        var key = Seq("WITH", Alt(def, kdef));

        var specified = Seq(Alt(type, like),
                              Opt(key),
                              Opt(initial),
                              Opt(length),
                              Opt(decimals));

        var ret = Seq("CREATE DATA",
                        Expr<Target>(),
                        Opt(Alt(typeHandle, Seq(Opt(areaHandle), specified))));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
