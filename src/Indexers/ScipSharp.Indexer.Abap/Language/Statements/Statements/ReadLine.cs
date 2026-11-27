// Ported from abaplint packages/core/src/abap/2_statements/statements/read_line.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class ReadLine : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var val = Seq("LINE VALUE INTO", Expr<Target>());

        var fields = Seq(Expr<Target>(), Opt(Seq("INTO", Expr<Target>())));

        var field = Seq("FIELD VALUE", Plus(fields));

        var index = Seq("INDEX", Expr<Source>());

        var page = Seq("OF PAGE", Expr<Source>());

        var current = Str("OF CURRENT PAGE");

        var ret = Seq("READ",
                        Alt("CURRENT LINE", Seq("LINE", Expr<Source>())),
                        Opt(Per(val, index, field, page, current)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
