// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_typed_literal.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
// Ported by hand: the TS source builds a RegExp at run time
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

public class SQLTypedLiteral : Expression
{
    private static readonly string[] TYPED_LITERAL_TYPES =
    [
        "CHAR", "CLNT", "CUKY", "CURR", "DATN", "DATS", "DEC",
        "DECFLOAT16", "DECFLOAT34",
        "D16N", "D34N", "D16D", "D34D", "D16R", "D34R",
        "FLTP",
        "INT1", "INT2", "INT4", "INT8",
        "LANG", "NUMC", "QUAN", "RAW", "RAWSTRING",
        "SSTRING", "STRING", "TIMN", "TIMS", "UNIT", "UTCLONG",
    ];

    public override IStatementRunnable GetRunnable()
    {
        var typeName = Regex("^(" + string.Join("|", TYPED_LITERAL_TYPES) + ")$", "i");
        return Seq(typeName, Regex(@"^`.*`$"));
    }
}
