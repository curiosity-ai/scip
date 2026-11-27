// Ported from abaplint packages/core/src/abap/2_statements/statements/create_ole.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CreateOLE : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("CREATE OBJECT",
                        Expr<Target>(),
                        Expr<Source>(),
                        Opt("NO FLUSH"),
                        Opt("QUEUE-ONLY"));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
