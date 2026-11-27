// Ported from abaplint packages/core/src/abap/2_statements/expressions/find_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FindType : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        return Opt(Alt("REGEX", "SUBSTRING", Ver(Release.v755, "PCRE", also: AlsoIn.OpenABAP)));
    }
}
