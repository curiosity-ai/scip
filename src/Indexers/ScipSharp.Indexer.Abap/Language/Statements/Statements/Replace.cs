// Ported from abaplint packages/core/src/abap/2_statements/statements/replace.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Replace : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var length = Seq("LENGTH", Expr<Source>());
        var offset = Seq("OFFSET", Expr<Source>());

        var section = Seq(Opt("IN"),
                            "SECTION",
                            Per(offset, length),
                            "OF",
                            Expr<Target>());

        var source = Seq(Opt("OF"), Expr<FindType>(), Expr<Source>());

        var cas = AltPrio("IGNORING CASE", "RESPECTING CASE");

        var repl = Seq("REPLACEMENT COUNT", Expr<Target>());
        var replo = Seq("REPLACEMENT OFFSET", Expr<Target>());
        var repll = Seq("REPLACEMENT LENGTH", Expr<Target>());
        var repli = Seq("REPLACEMENT LINE", Expr<Target>());

        var occ = AltPrio("ALL OCCURRENCES",
                            "ALL OCCURENCES",
                            "FIRST OCCURENCE",
                            "FIRST OCCURRENCE");

        var mode = Alt("IN CHARACTER MODE",
                         "IN BYTE MODE");

        var wit = Seq("WITH", Expr<Source>());
        var into = Seq("INTO", Expr<Target>());

        return Seq("REPLACE",
                   Per(section, Seq(Opt(occ), source)),
                   Opt(Seq("IN", Opt("TABLE"), Expr<Target>())),
                   Opt(Per(wit, into, cas, mode, repl, replo, repll, repli, length)));
    }
}
