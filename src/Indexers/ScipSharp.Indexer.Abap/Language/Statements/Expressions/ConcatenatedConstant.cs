// Ported from abaplint packages/core/src/abap/2_statements/expressions/concatenated_constant.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ConcatenatedConstant : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var str = Seq(Regex(@"^`.*`$"), PlusPrio(Seq("&", Regex(@"^`.*`$"))));
        var @char = Seq(Regex(@"^'.*'$"), PlusPrio(Seq("&", Regex(@"^'.*'$"))));
        return AltPrio(str, @char);
    }
}
