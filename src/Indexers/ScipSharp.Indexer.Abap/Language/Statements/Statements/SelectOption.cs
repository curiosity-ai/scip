// Ported from abaplint packages/core/src/abap/2_statements/statements/selectoption.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SelectOption : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var sourc = Alt(Expr<Expressions.Constant>(), Expr<FieldChain>());

        var to = Seq("TO", sourc);

        var def = Seq("DEFAULT",
                        sourc,
                        Opt(to));

        var option = Seq("OPTION", Alt("CP", Expr<Expressions.Field>()));
        var sign = Seq("SIGN", Expr<Expressions.Field>());

        var memory = Seq("MEMORY ID", Expr<SimpleSource1>());

        var match = Seq("MATCHCODE OBJECT", Expr<SimpleFieldChain>());

        var modif = Seq("MODIF ID", Expr<Modif>());

        var visible = Seq("VISIBLE LENGTH", Expr<Source>());

        var options = Per(def,
                            option,
                            sign,
                            memory,
                            match,
                            visible,
                            modif,
                            "NO DATABASE SELECTION",
                            "LOWER CASE",
                            "NO-EXTENSION",
                            "NO INTERVALS",
                            "NO-DISPLAY",
                            "OBLIGATORY");

        var ret = Seq("SELECT-OPTIONS",
                        Expr<FieldSub>(),
                        "FOR",
                        Alt(Expr<FieldChain>(), Expr<Dynamic>()),
                        Opt(options));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
