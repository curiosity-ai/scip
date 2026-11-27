// Ported from abaplint packages/core/src/abap/2_statements/expressions/write_offset_length.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class WriteOffsetLength : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var post = Seq(Alt(Expr<SimpleFieldChain2>(), Regex(@"^[\d]+$"), Regex(@"^\*$")), Alt(Tok<Tokens.ParenRightW>(), Tok<Tokens.ParenRight>()));
        var wlength = Seq(Tok<Tokens.WParenLeft>(), post);
        var length = Seq(Alt(Tok<Tokens.WParenLeft>(), Tok<Tokens.ParenLeft>()), post);

        var complex = Alt(wlength,
                            Seq(Alt(Expr<SimpleFieldChain2>(), Regex(@"^\/?[\w\d]+$"), "/"), Opt(length)));

        var at = Seq(Opt("AT"), complex);

        return at;
    }
}
