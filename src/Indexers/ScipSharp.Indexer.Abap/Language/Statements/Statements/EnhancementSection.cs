// Ported from abaplint packages/core/src/abap/2_statements/statements/enhancement_section.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class EnhancementSection : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Seq("ENHANCEMENT-SECTION",
                        Seq(Expr<Expressions.Field>(), StarPrio(Seq(Tok<Tokens.Dash>(), Expr<Expressions.Field>()))),
                        "SPOTS",
                        Expr<Expressions.Field>(),
                        Opt("STATIC"),
                        Opt("INCLUDE BOUND"));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
