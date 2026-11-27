// Ported from abaplint packages/core/src/abap/2_statements/statements/enhancement_point.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class EnhancementPoint : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("ENHANCEMENT-POINT",
                        Expr<FieldSub>(),
                        "SPOTS",
                        Expr<Expressions.Field>(),
                        Opt("STATIC"),
                        Opt("INCLUDE BOUND"));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
