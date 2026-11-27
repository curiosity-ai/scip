// Ported from abaplint packages/core/src/abap/2_statements/expressions/source_field.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SourceField : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        return new Field();
    }
}
