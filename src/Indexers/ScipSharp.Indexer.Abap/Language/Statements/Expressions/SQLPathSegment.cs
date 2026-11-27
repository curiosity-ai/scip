// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_path_segment.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLPathSegment : Expression
{
    private readonly bool nws;

    public SQLPathSegment() : this(false)
    {
    }

    public SQLPathSegment(bool nws)
    {
        this.nws = nws;
    }

    public override IStatementRunnable GetRunnable()
    {
        var joinTag = OptPrio(Ver(Release.v795, Regex(@"^#[A-Z][A-Z0-9_]*$", "i")));
        var filter = Ver(Release.v751, Seq(
          Tok<Tokens.BracketLeftW>(),
          joinTag,
          OptPrio(Expr<SQLPathCardinality>()),
          OptPrio(Expr<SQLPathJoinType>()),
          OptPrio(Seq(OptPrio("WHERE"), Expr<SQLCond>())),
          AltPrio(Tok<Tokens.WBracketRightW>(), Tok<Tokens.WBracketRight>())));
        var @params = Ver(Release.v751, Expr<SQLCDSParameters>());

        var name = this.nws
            ? Tok<Tokens.AssociationName>()
            : AltPrio(Tok<Tokens.AssociationName>(), Regex(@"^\\[\w]+$"));

        return Ver(Release.v740sp05, Seq(name, OptPrio(AltPrio(
          Seq(@params, OptPrio(filter)),
          filter))));
    }
}
