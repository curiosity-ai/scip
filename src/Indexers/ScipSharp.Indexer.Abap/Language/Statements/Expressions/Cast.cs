// Ported from abaplint packages/core/src/abap/2_statements/expressions/cast.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Cast : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var rparen = AltPrio(Tok<Tokens.WParenRightW>(), Tok<Tokens.WParenRight>());

        var cast = Seq("CAST", Expr<TypeNameOrInfer>(), Tok<Tokens.ParenLeftW>(), OptPrio(Expr<Let>()), Expr<Source>(), rparen, Opt(Expr<Dereference>()));

        return Ver(Release.v740sp02, cast, also: AlsoIn.OpenABAP);
    }
}
