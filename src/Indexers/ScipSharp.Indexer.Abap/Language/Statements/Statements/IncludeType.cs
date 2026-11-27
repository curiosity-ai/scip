// Ported from abaplint packages/core/src/abap/2_statements/statements/include_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class IncludeType : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var tas = Seq("AS", Expr<Expressions.Field>());

        var renaming = Seq("RENAMING WITH SUFFIX", Expr<ComponentName>());

        var ret = Seq("INCLUDE",
                        Alt("TYPE", "STRUCTURE"),
                        Expr<TypeName>(),
                        Opt(tas),
                        Opt(renaming));

        return ret;
    }
}
