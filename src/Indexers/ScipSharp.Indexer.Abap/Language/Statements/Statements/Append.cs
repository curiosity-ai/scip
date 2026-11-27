// Ported from abaplint packages/core/src/abap/2_statements/statements/append.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Append : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var assigning = Seq("ASSIGNING", Expr<FSTarget>());
        var reference = Seq("REFERENCE INTO", Expr<Target>());
        var sorted = Seq("SORTED BY", Expr<Expressions.Field>());

        var fromIndex = Seq("FROM", Expr<Source>());
        var toIndex = Seq("TO", Expr<Source>());
        var toTarget = Seq("TO", Expr<Target>());

        var src = Alt(Expr<SimpleSource4>(), Ver(Release.v740sp02, Expr<Source>(), also: AlsoIn.OpenABAP));

        return Seq("APPEND",
                   AltPrio("INITIAL LINE", Seq(OptPrio("LINES OF"), src)),
                   OptPrio(fromIndex),
                   Opt(Alt(Seq(toIndex, toTarget), toTarget)),
                   Opt(AltPrio(assigning, reference)),
                   OptPrio("CASTING"),
                   OptPrio(sorted));
    }
}
