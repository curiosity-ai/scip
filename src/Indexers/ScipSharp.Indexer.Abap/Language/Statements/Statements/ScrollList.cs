// Ported from abaplint packages/core/src/abap/2_statements/statements/scroll_list.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ScrollList : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var index = Seq("INDEX", Expr<Source>());
        var line = Seq("LINE", Expr<Source>());
        var column = Seq("TO COLUMN", Expr<Source>());

        var to = Seq("TO",
                       Alt("FIRST PAGE",
                           "LAST PAGE",
                           Seq("PAGE", Expr<Source>())));

        var pages = Seq(Expr<Source>(), "PAGES");

        var ret = Seq("SCROLL LIST",
                        Per(index,
                            Seq(Alt(to, "BACKWARD", "FORWARD"), Opt(pages)),
                            Seq(Alt("LEFT", "RIGHT"), Opt(Seq("BY", Expr<Source>(), "PLACES"))),
                            column,
                            line));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
