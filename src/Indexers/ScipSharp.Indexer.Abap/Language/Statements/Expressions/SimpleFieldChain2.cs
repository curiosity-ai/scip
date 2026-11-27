// Ported from abaplint packages/core/src/abap/2_statements/expressions/simple_field_chain2.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SimpleFieldChain2 : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var attr = Seq(Tok<Tokens.InstanceArrow>(), Expr<AttributeName>());
        var comp = Seq(Tok<Tokens.Dash>(), Expr<ComponentName>());

        var chain = Star(AltPrio(attr, comp));

        var clas = Seq(Expr<ClassName>(), Tok<Tokens.StaticArrow>(), Expr<AttributeName>());
        var start = AltPrio(clas, Expr<SourceField>(), Expr<SourceFieldSymbol>());

        var ret = Seq(start, chain);

        return ret;
    }
}
