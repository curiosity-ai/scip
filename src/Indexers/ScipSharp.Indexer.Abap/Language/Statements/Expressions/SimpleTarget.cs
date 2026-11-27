// Ported from abaplint packages/core/src/abap/2_statements/expressions/simple_target.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SimpleTarget : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var attr = Seq(Tok<Tokens.InstanceArrow>(), Expr<AttributeName>());
        var comp = Seq(Tok<Tokens.Dash>(), Expr<ComponentName>());

        var something = StarPrio(AltPrio(Expr<Dereference>(), attr, comp, Expr<TableExpression>()));

        var cast = Seq(AltPrio(Expr<Cast>(), Expr<NewObject>()), Expr<Arrow>(), Expr<FieldAll>());

        var clas = Seq(Expr<ClassName>(), Tok<Tokens.StaticArrow>(), Expr<AttributeName>());
        var start = AltPrio(cast, clas, Expr<TargetField>(), Expr<TargetFieldSymbol>());

        var fields = Seq(OptPrio(Expr<FieldOffset>()), OptPrio(Expr<FieldLength>()));

        var optional = AltPrio(Expr<TableBody>(), fields);

        return Seq(start, something, optional);
    }
}
