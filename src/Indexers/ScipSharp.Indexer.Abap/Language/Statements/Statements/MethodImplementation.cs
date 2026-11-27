// Ported from abaplint packages/core/src/abap/2_statements/statements/method_implementation.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodImplementation : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var name = Regex(@"[\w~]+");

        var kernel = Seq("KERNEL MODULE",
                           Plus(name),
                           OptPrio(AltPrio("FAIL", "IGNORE")));

        var @using = Seq("USING", Plus(Expr<SimpleFieldChain>()));

        var options = Seq("OPTIONS", Per("READ-ONLY", "SUPPRESS SYNTAX ERRORS", "DETERMINISTIC"));

        var database = Seq("DATABASE", Alt("PROCEDURE", "FUNCTION", "GRAPH WORKSPACE"), "FOR HDB",
                             Expr<Expressions.Language>(),
                             Opt(options),
                             Opt(@using));

        // BY DATABASE and BY KERNEL both blocked in KeyUser
        var by = VerNotLang(LanguageVersion.KeyUser, Seq("BY", Alt(kernel, database)));

        return Seq("METHOD", Expr<MethodName>(), OptPrio(by));
    }
}
