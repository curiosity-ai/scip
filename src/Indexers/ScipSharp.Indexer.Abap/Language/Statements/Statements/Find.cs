// Ported from abaplint packages/core/src/abap/2_statements/statements/find.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Find : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        // SUBMATCHES handling is a workaround
        var options = Per("IGNORING CASE",
                            "RESPECTING CASE",
                            "IN BYTE MODE",
                            "IN CHARACTER MODE",
                            Seq("OF", Expr<Source>()),
                            Seq("FROM", Expr<Source>()),
                            Seq("TO", Expr<Source>()),
                            Seq("MATCH OFFSET", Expr<Target>()),
                            Seq("MATCH LINE", Expr<Target>()),
                            Seq("MATCH COUNT", Expr<Target>()),
                            Seq("MATCH LENGTH", Expr<Target>()),
                            Seq("LENGTH", Expr<Source>()),
                            Seq("RESULTS", Expr<Target>()),
                            Seq("SUBMATCHES", Expr<Target>()),
                            Seq("SUBMATCHES", Expr<Target>(), Expr<Target>()),
                            Seq("SUBMATCHES", Plus(Expr<Target>())));

        var sectionLength = Seq("SECTION LENGTH", Expr<Source>(), "OF");

        var before = Seq(OptPrio(AltPrio("TABLE", "SECTION OFFSET", sectionLength)),
                           Expr<Source>());

        var ret = Seq("FIND",
                        Opt(AltPrio("FIRST OCCURRENCE OF", "ALL OCCURRENCES OF")),
                        Expr<FindType>(),
                        Expr<Source>(),
                        "IN",
                        before,
                        Opt(options));

        return ret;
    }
}
