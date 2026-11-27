// Ported from abaplint packages/core/src/abap/2_statements/statements/get_cursor.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GetCursor : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var line = Seq("LINE", Expr<Target>());
        var field = Seq("FIELD", Expr<Target>());
        var offset = Seq("OFFSET", Expr<Target>());
        var value = Seq("VALUE", Expr<Target>());
        var length = Seq("LENGTH", Expr<Target>());
        var area = Seq("AREA", Expr<Target>());

        var ret = Seq("GET CURSOR",
                        Per(line, Opt("DISPLAY"), field, offset, value, length, area));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
