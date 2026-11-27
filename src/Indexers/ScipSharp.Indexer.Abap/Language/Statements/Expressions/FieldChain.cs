// Ported from abaplint packages/core/src/abap/2_statements/expressions/field_chain.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FieldChain : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var attr = Seq(Tok<Tokens.InstanceArrow>(), Expr<AttributeName>());
        var comp = Seq(Tok<Tokens.Dash>(), OptPrio(Expr<ComponentName>()));

        var chain = Star(AltPrio(
          Expr<Dereference>(),
          DynamicAccess.DynAttr(),
          attr,
          DynamicAccess.DynComp(OptPrio(Expr<FieldOffset>()), OptPrio(Expr<FieldLength>())),
          comp,
          Tok<Tokens.AssociationName>(),
          Expr<TableExpression>()));

        var clas = Seq(Expr<ClassName>(), Tok<Tokens.StaticArrow>(), Expr<AttributeName>());
        var start = AltPrio(clas, Expr<SourceField>(), Expr<SourceFieldSymbol>());

        var after = AltPrio(Tok<Tokens.DashW>(), Seq(OptPrio(Expr<TableBody>()), OptPrio(Expr<FieldOffset>()), OptPrio(Expr<FieldLength>())));

        var ret = Seq(start, chain, after);

        return ret;
    }
}
