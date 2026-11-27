// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_call_chain.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodCallChain : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var attr = Seq(Tok<Tokens.InstanceArrow>(), Expr<AttributeName>());
        var comp = Seq(Tok<Tokens.Dash>(), Expr<ComponentName>());

        var fields = Star(AltPrio(attr, comp));

        var after = Star(Seq(fields, Tok<Tokens.InstanceArrow>(), Expr<MethodCall>()));

        var localVariable = Seq(Expr<FieldChain>(), Tok<Tokens.InstanceArrow>());
        var staticClass = Seq(Expr<ClassName>(), Tok<Tokens.StaticArrow>());

        var ret = Seq(AltPrio(Seq(OptPrio(AltPrio(localVariable, staticClass)), Expr<MethodCall>()),
                                Expr<NewObject>(),
                                Expr<Cast>()),
                        after);

        return ret;
    }
}
