// Ported from abaplint packages/core/src/abap/2_statements/statements/parameter.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Parameter : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var para = AltPrio("PARAMETER", "PARAMETERS");
        var def = Seq("DEFAULT", AltPrio(Expr<Expressions.Constant>(), Expr<FieldChain>()));
        var radio = Seq("RADIOBUTTON GROUP", Expr<RadioGroupName>());
        var type = Seq(AltPrio("TYPE", "LIKE"), AltPrio(Expr<TypeName>(), Expr<Dynamic>()), OptPrio(Expr<FieldOffset>()), OptPrio(Expr<FieldLength>()));
        var memory = Seq("MEMORY ID", Expr<SimpleSource1>());
        var listbox = Str("AS LISTBOX");
        var cmd = Seq("USER-COMMAND", Regex(@"^[\w\?\/]+$"));
        var modif = Seq("MODIF ID", Expr<Modif>());
        var visible = Seq("VISIBLE LENGTH", Expr<Expressions.Constant>());
        var length = Seq("LENGTH", Expr<Expressions.Constant>());
        var match = Seq("MATCHCODE OBJECT", Expr<Expressions.Field>());
        var decimals = Seq("DECIMALS", Expr<Source>());
        var forTable = Seq("FOR TABLE", Expr<DatabaseTable>(), Opt("VALUE-REQUEST"));

        var perm = Per(type,
                         def,
                         "OBLIGATORY",
                         match,
                         cmd,
                         length,
                         decimals,
                         radio,
                         memory,
                         modif,
                         listbox,
                         visible,
                         forTable,
                         "VALUE CHECK",
                         "NO-DISPLAY",
                         "AS CHECKBOX",
                         "LOWER CASE");

        var ret = Seq(para,
                        Expr<FieldSub>(),
                        Opt(Expr<ConstantFieldLength>()),
                        Opt(perm));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
