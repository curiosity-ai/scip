// Ported from abaplint packages/core/src/abap/2_statements/expressions/default.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Default : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        // todo, DEFAULT is only valid for definitions in relation to method parameters
        var def = Seq("DEFAULT", AltPrio(Expr<TextElement>(), Expr<Constant>(), Expr<FieldChain>()));

        return def;
    }
}
