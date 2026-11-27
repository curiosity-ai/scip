// Ported from abaplint packages/core/src/abap/2_statements/expressions/color.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Color : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var eq = Seq("=", Expr<SimpleSource3>());
        var integers = AltPrio("1", "2", "3", "4", "5", "6", "7");
        var texts = AltPrio("COL_BACKGROUND",
                              "COL_HEADING",
                              "COL_NORMAL",
                              "COL_TOTAL",
                              "COL_KEY",
                              "COL_POSITIVE",
                              "COL_NEGATIVE",
                              "COL_GROUP");
        var value = Alt(eq, AltPrio("ON", "OFF", "COLOR OFF", AltPrio(integers, texts)));
        var toggle = AltPrio("ON", "OFF");

        return Seq("COLOR", Opt(Seq(value, Opt(toggle))));
    }
}
