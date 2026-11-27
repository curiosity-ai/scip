// Ported from abaplint packages/core/src/abap/2_statements/statements/sort.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Sort : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var order = AltPrio("ASCENDING", "DESCENDING");

        var sel = Alt(Expr<ComponentChain>(), Expr<Dynamic>(), Expr<SourceFieldSymbol>());

        string text = "AS TEXT";

        var fields = Plus(Seq(sel, OptPrio(text), OptPrio(order), OptPrio(text)));

        var by = Seq("BY", fields);

        var normal = Seq(Expr<Target>(), Opt(Per(order, by, "STABLE", text)));

        return Seq("SORT", normal);
    }
}
