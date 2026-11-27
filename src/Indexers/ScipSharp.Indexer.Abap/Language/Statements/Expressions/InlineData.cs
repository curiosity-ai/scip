// Ported from abaplint packages/core/src/abap/2_statements/expressions/inlinedata.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class InlineData : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var right = AltPrio(Tok<Tokens.ParenRightW>(), Tok<Tokens.ParenRight>());
        var left = Tok<Tokens.ParenLeft>();
        var data = Seq("DATA", left, Expr<TargetField>(), right);
        var final = Seq("FINAL", left, Expr<TargetField>(), right);

        return AltPrio(Ver(Release.v740sp02, data, also: AlsoIn.OpenABAP), Ver(Release.v757, final, also: AlsoIn.OpenABAP));
    }
}
