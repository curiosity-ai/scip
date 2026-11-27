// Ported from abaplint packages/core/src/abap/2_statements/statements/commit_entities.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CommitEntities : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var failed = Seq("FAILED", Expr<Target>());
        var reported = Seq("REPORTED", Expr<Target>());

        var responses = Seq("RESPONSES", failed, reported);

        var s = Seq("COMMIT ENTITIES",
                      OptPrio("IN SIMULATION MODE"),
                      Opt(responses),
                      Opt(Seq("RESPONSE OF", Expr<NamespaceSimpleName>(), Per(failed, reported))));

        return VerNotLang(LanguageVersion.KeyUser, Ver(Release.v754, s, also: AlsoIn.OpenABAP));
    }
}
