// Ported from abaplint packages/core/src/abap/2_statements/expressions/corresponding_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CorrespondingBody : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var discarding = Ver(Release.v751, "DISCARDING DUPLICATES");

        return Seq(
          OptPrio("DEEP"),
          OptPrio(Expr<CorrespondingBodyBase>()),
          Expr<Source>(),
          OptPrio(discarding),
          OptPrio(Expr<CorrespondingBodyMapping>()),
          OptPrio("CHANGING CONTROL"),
          OptPrio(Seq("MAPPING FROM ENTITY", OptPrio("USING CONTROL"))),
          OptPrio(Seq("EXCEPT", Alt(Plus(Expr<Field>()), "*"))));
    }
}
