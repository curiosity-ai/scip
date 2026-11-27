// Ported from abaplint packages/core/src/abap/2_statements/expressions/simple_source1.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SimpleSource1 : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var concat = Seq("&&", Expr<StringTemplate>());

        var template = Seq(Expr<StringTemplate>(), Star(concat));

        return Alt(Expr<Constant>(), Expr<TextElement>(), Expr<MethodCallChain>(), template, Expr<FieldChain>());
    }
}
