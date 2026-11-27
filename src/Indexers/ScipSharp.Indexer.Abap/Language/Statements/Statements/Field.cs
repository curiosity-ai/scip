// Ported from abaplint packages/core/src/abap/2_statements/statements/field.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Field : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var moduleOptions = Alt("ON INPUT", "ON REQUEST", "ON CHAIN-REQUEST", "ON CHAIN-INPUT", "AT CURSOR-SELECTION");
        var module = Seq("MODULE", Expr<FormName>(), Opt(moduleOptions));
        var moduleStrange = Seq(moduleOptions, "MODULE", Expr<FormName>());
        var values = Seq("VALUES", Tok<Tokens.WParenLeft>(), "BETWEEN", Expr<Expressions.Constant>(), "AND", Expr<Expressions.Constant>(), Tok<Tokens.ParenRightW>());
        var wit = Seq("WITH", AltPrio(Expr<FieldChain>(), Expr<Expressions.Constant>()));

        var cond = Seq(Expr<FieldChain>(), "=", Expr<FieldChain>());
        var where = Seq(cond, StarPrio(Seq("AND", cond)));
        var into = Seq("INTO", Expr<FieldChain>());
        var select = Seq("SELECT * FROM", Expr<FieldChain>(), "WHERE", where, Opt(into), Opt("WHENEVER NOT FOUND SEND ERRORMESSAGE"));

        var ret = Seq("FIELD", Expr<FieldChain>(), Opt(AltPrio(module, moduleStrange, values, wit, select)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
