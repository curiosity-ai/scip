// Ported from abaplint packages/core/src/abap/2_statements/expressions/value_body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ValueBody : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var strucOrTab = Seq(OptPrio(Expr<Let>()), OptPrio(Expr<ValueBase>()), StarPrio(Expr<For>()), PlusPrio(AltPrio(Expr<FieldAssignment>(), Expr<ValueBodyLine>())));

        var tabdef = Ver(Release.v740sp08, AltPrio("OPTIONAL", Seq("DEFAULT", Expr<Source>())), also: AlsoIn.OpenABAP);

        return OptPrio(AltPrio(strucOrTab, Seq(Expr<Source>(), OptPrio(tabdef))));
    }
}
