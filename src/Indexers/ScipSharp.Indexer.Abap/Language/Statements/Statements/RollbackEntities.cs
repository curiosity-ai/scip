// Ported from abaplint packages/core/src/abap/2_statements/statements/rollback_entities.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class RollbackEntities : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        string s = "ROLLBACK ENTITIES";
        return VerNotLang(LanguageVersion.KeyUser, Ver(Release.v754, s, also: AlsoIn.OpenABAP));
    }
}
