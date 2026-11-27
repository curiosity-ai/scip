// Ported from abaplint packages/core/src/abap/2_statements/expressions/field.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Field : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // "&1" can be used for almost anything(field names, method names etc.) in macros
        // field names with only digits should not be possible
        return Regex(@"^[&_!?]?\*?\w*(\/\w+\/)?\d*[a-zA-Z_%\$][\w\*%\$\?#]*(~\w+)?$");
    }
}
