// Ported from abaplint packages/core/src/abap/2_statements/expressions/compare_operator.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CompareOperator : Expression
{
    public override IStatementRunnable GetRunnable()
    {

        var @operator = AltPrio("=",
                                 "<>",
                                 "><",
                                 "<",
                                 ">",
                                 "<=",
                                 ">=",
                                 "=>",
                                 "=<",
                                 "CA",
                                 "CO",
                                 "CP",
                                 "EQ",
                                 "NE",
                                 "CN",
                                 "GE",
                                 "GT",
                                 "LT",
                                 "LE",
                                 "CS",
                                 "NS",
                                 "NA",
                                 "NP",
                                 "BYTE-CO",
                                 "BYTE-CA",
                                 "BYTE-CS",
                                 "BYTE-CN",
                                 "BYTE-NA",
                                 "BYTE-NS",
                                 "O",
                                 "Z",
                                 "M");
        return @operator;
    }
}
