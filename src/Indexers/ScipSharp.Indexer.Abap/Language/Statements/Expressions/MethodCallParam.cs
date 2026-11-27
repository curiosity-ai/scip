// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_call_param.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodCallParam : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var param = Alt(Expr<Source>(), Expr<ParameterListS>(), Expr<MethodParameters>());

        // rule ParserMissingSpace makes sure the whitespace is correct
        var right1 = AltPrio(Tok<Tokens.WParenRight>(), Tok<Tokens.WParenRightW>(), Tok<Tokens.ParenRight>(), Tok<Tokens.ParenRightW>());
        var right2 = AltPrio(Tok<Tokens.WParenRight>(), Tok<Tokens.WParenRightW>());

        // note: make sure this does not overlap with FieldLength expression
        var ret = AltPrio(
          Seq(Tok<Tokens.ParenLeftW>(), param, right1),
          Seq(Tok<Tokens.ParenLeft>(), param, right2),
          Seq(Tok<Tokens.ParenLeft>(), Expr<ConstantString>(), AltPrio(Tok<Tokens.ParenRight>(), Tok<Tokens.ParenRightW>())));
        return ret;
    }
}
