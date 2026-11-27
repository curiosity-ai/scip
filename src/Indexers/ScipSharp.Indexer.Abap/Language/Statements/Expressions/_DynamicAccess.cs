// Ported from abaplint packages/core/src/abap/2_statements/expressions/_dynamic_access.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public static class DynamicAccess
{

    public static readonly IStatementRunnable dynIdent = Tok<Tokens.ParenLeft>();
    public static readonly IStatementRunnable dynClose = AltPrio(Tok<Tokens.ParenRight>(), Tok<Tokens.ParenRightW>());
    public static readonly IStatementRunnable dynInside = AltPrio(new FieldChain(), Expr<Constant>());

    public static IStatementRunnable DynComp(params IStatementRunnable[] inside)
    {
        return Ver(Release.v757, SeqOf([Tok<Tokens.Dash>(), dynIdent, dynInside, .. inside, dynClose]));
    }

    public static IStatementRunnable DynAttr()
    {
        return Ver(Release.v757, Seq(Tok<Tokens.InstanceArrow>(), dynIdent, dynInside, dynClose));
    }
}
