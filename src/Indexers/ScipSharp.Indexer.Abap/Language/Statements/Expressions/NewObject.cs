// Ported from abaplint packages/core/src/abap/2_statements/expressions/new_object.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

// note: this can also be new data reference
public class NewObject : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var lines = Plus(Seq(Tok<Tokens.WParenLeftW>(), Expr<Source>(), Tok<Tokens.WParenRightW>()));
        var linesFields = Plus(Seq(Tok<Tokens.WParenLeftW>(), Plus(Expr<FieldAssignment>()), Tok<Tokens.WParenRightW>()));

        var neww = Seq("NEW",
                         Expr<TypeNameOrInfer>(),
                         Tok<Tokens.ParenLeftW>(),
                         OptPrio(Alt(Expr<Source>(), Expr<ParameterListS>(), lines, linesFields)),
                         ")");

        return Ver(Release.v740sp02, neww, also: AlsoIn.OpenABAP);
    }
}
