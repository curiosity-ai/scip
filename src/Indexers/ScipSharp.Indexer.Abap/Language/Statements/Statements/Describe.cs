// Ported from abaplint packages/core/src/abap/2_statements/statements/describe.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Describe : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var tlines = Seq("LINES", Expr<Target>());
        var kind = Seq("KIND", Expr<Target>());
        var occurs = Seq("OCCURS", Expr<Target>());

        var table = Seq("TABLE",
                          Expr<FieldChain>(),
                          Opt(Per(tlines, kind, occurs)));

        var mode = Seq("IN", Alt("BYTE", "CHARACTER"), "MODE");

        var field = Seq("FIELD",
                          Expr<Source>(),
                          Per(Seq("TYPE", Expr<Target>()),
                              Seq("COMPONENTS", Expr<Target>()),
                              Seq("LENGTH", Expr<Target>(), Opt(mode)),
                              Seq("DECIMALS", Expr<Target>()),
                              Seq("HELP-ID", Expr<Target>()),
                              Seq("OUTPUT-LENGTH", Expr<Target>()),
                              Seq("EDIT MASK", Expr<Target>()),
                              Seq("INTO", Expr<Target>())));

        var distance = Seq("DISTANCE BETWEEN",
                             Expr<Source>(),
                             "AND",
                             Expr<Source>(),
                             "INTO",
                             Expr<Target>(),
                             Opt(mode));

        var lines = Seq("NUMBER OF LINES", Expr<Target>());
        var pages = Seq("NUMBER OF PAGES", Expr<Target>());
        var line = Seq("LINE", Expr<Source>());
        var page = Seq("PAGE", Expr<Source>());
        var index = Seq("INDEX", Expr<Source>());
        var top = Seq("TOP-LINES", Expr<Target>());
        var lineSize = Seq("LINE-SIZE", Expr<Target>());
        var lineCount = Seq("LINE-COUNT", Expr<Target>());
        var first = Seq("FIRST-LINE", Expr<Target>());

        var list = Seq("LIST", Per(lines, pages, index, line, page, top, first, tlines, lineSize, lineCount));

        var ret = Seq("DESCRIBE", AltPrio(table, field, distance, list));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
