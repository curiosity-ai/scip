// Ported from abaplint packages/core/src/abap/2_statements/expressions/assign_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class AssignSource : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var component = Seq("COMPONENT",
                              Alt(Expr<SimpleSource3>(), Ver(Release.v740sp02, Expr<Source>(), also: AlsoIn.OpenABAP)),
                              "OF STRUCTURE",
                              Expr<Source>());

        // TABLE FIELD form: blocked in KeyUser
        var tableField = VerNotLang(LanguageVersion.KeyUser, Seq("TABLE FIELD", Alt(Expr<Source>(), Expr<Dynamic>())));

        // class=>(dyn_attr) via static arrow is blocked in KeyUser
        // ref->(dyn_comp) via instance arrow is allowed — leave unguarded
        var arrowDynamic = Alt(
          Tok<Tokens.InstanceArrow>(),
          VerNotLang(LanguageVersion.KeyUser, Tok<Tokens.StaticArrow>()));

        var anyArrow = Alt(Tok<Tokens.InstanceArrow>(), Tok<Tokens.StaticArrow>());
        var source = Alt(Seq(Expr<Source>(), Opt(Seq(arrowDynamic, Expr<Dynamic>()))),
                           component,
                           tableField,
                           VerNotLang(LanguageVersion.KeyUser,
                                      Seq(Expr<Dynamic>(), Opt(Seq(anyArrow, Alt(Expr<Field>(), Expr<Dynamic>()))))));

        return source;
    }
}
