// Ported from abaplint packages/core/src/abap/2_statements/statements/sort_dataset.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SortDataset : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var order = Alt("ASCENDING", "DESCENDING");

        var sel = Alt(Expr<ComponentChain>(), Expr<SourceFieldSymbol>(), Expr<Dynamic>());

        var fields = Plus(Seq(sel, OptPrio(order)));

        var by = Seq("BY", fields);

        var ret = Seq("SORT", Opt("AS TEXT"), Opt(by));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
