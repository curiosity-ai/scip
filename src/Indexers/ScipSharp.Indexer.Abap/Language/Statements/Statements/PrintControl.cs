// Ported from abaplint packages/core/src/abap/2_statements/statements/print_control.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class PrintControl : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var index = Seq("INDEX-LINE", Expr<Source>());
        var func = Seq("FUNCTION", Expr<Source>());
        var line = Seq("LINE", Expr<Source>());
        var position = Seq("POSITION", Expr<Source>());
        var size = Seq("SIZE", Expr<Source>());
        var cpi = Seq("CPI", Expr<Source>());
        var lpi = Seq("LPI", Expr<Source>());
        var font = Seq("FONT", Expr<Source>());

        var ret = Seq("PRINT-CONTROL", Per(index, func, line, position, size, cpi, lpi, font));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
