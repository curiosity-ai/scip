// Ported from abaplint packages/core/src/abap/2_statements/expressions/table_expression.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TableExpression : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var fields = Plus(Seq(AltPrio(Expr<ComponentChainSimple>(), Expr<Dynamic>()), "=", Expr<Source>()));
        var key = Seq("KEY", Expr<SimpleName>());
        var index = Seq("INDEX", Expr<Source>());
        var ret = Seq(Tok<Tokens.BracketLeftW>(),
                        Alt(Seq(Expr<Source>(), fields), Expr<Source>(), Seq(OptPrio(key), Opt("COMPONENTS"), AltPrio(fields, index))),
                        AltPrio(Tok<Tokens.WBracketRight>(), Tok<Tokens.WBracketRightW>()));
        return Ver(Release.v740sp02, ret, also: AlsoIn.OpenABAP);
    }
}
