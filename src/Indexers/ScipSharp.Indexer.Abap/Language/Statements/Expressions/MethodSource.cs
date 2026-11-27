// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_source.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodSource : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        // note: AttributeName can be both an attribute and a method name, the syntax check will tell
        // note: its allowed to end with MethodCall, however if this is done it will give a syntax error via syntax check
        var afterArrow = Alt(Expr<AttributeName>(), Expr<MethodCall>(), Expr<Dynamic>());
        var arrow = AltPrio(Tok<Tokens.InstanceArrow>(), Tok<Tokens.StaticArrow>());
        var attr = Seq(arrow, afterArrow);
        var comp = Seq(Tok<Tokens.Dash>(), Expr<ComponentName>());
        var attrOrComp = AltPrio(attr, comp);
        var staticClass = Seq(Expr<ClassName>(), Tok<Tokens.StaticArrow>());
        var clas = Seq(staticClass, afterArrow);

        var start = Seq(AltPrio(clas, Expr<SourceField>(), Expr<SourceFieldSymbol>(), Expr<Dynamic>()), Star(attrOrComp));

        return start;
    }
}
