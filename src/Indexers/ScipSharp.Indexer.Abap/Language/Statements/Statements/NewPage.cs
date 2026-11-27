// Ported from abaplint packages/core/src/abap/2_statements/statements/new_page.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class NewPage : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var line = Seq("LINE-SIZE", Expr<Source>());
        var print = Seq("PRINT", Alt("OFF", "ON"));
        var parameters = Seq("PARAMETERS", Expr<Source>());
        var destination = Seq("DESTINATION", Expr<Source>());
        var archive = Seq("ARCHIVE PARAMETERS", Expr<Source>());
        var lineCount = Seq("LINE-COUNT", Expr<Source>());
        var coverText = Seq("COVER TEXT", Expr<Source>());
        var coverPage = Seq("SAP COVER PAGE", Expr<Source>());
        var immediately = Seq("IMMEDIATELY", Expr<Source>());
        var copies = Seq("COPIES", Expr<Source>());
        var keep = Seq("KEEP IN SPOOL", Expr<Source>());
        var layout = Seq("LAYOUT", Expr<Source>());
        var listAuth = Seq("LIST AUTHORITY", Expr<Source>());
        var department = Seq("DEPARTMENT", Expr<Source>());
        var receiver = Seq("RECEIVER", Expr<Source>());
        var dataset = Seq("LIST DATASET", Expr<Source>());
        var name = Seq("LIST NAME", Expr<Source>());
        var newList = Seq("NEW LIST IDENTIFICATION", Expr<Source>());

        var ret = Seq("NEW-PAGE",
                        Opt(Per(print,
                                Alt("NO-TITLE", "WITH-TITLE"),
                                Alt("NO-HEADING", "WITH-HEADING"),
                                "NO DIALOG",
                                "NO-TOPOFPAGE",
                                parameters,
                                listAuth,
                                immediately,
                                dataset,
                                coverPage,
                                newList,
                                keep,
                                department,
                                name,
                                layout,
                                destination,
                                receiver,
                                copies,
                                coverText,
                                archive,
                                "NEW-SECTION",
                                lineCount,
                                line)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
