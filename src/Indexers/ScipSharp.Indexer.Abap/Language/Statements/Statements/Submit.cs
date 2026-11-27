// Ported from abaplint packages/core/src/abap/2_statements/statements/submit.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Submit : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var sign = Seq("SIGN", Expr<Source>());
        var eq = Alt("=", "EQ", "IN", "NE", "CP", "GE", "LE", "INCL", "GT", "LT");
        var compare = Seq(eq, Expr<Source>());
        var between = Seq("BETWEEN", Expr<Source>(), "AND", Expr<Source>());
        var selectionTable = Seq("WITH SELECTION-TABLE", Expr<Source>());
        var awith = Seq("WITH", Expr<FieldSub>(), Alt(compare, between), OptPrio(sign));
        var prog = AltPrio(Expr<IncludeName>(), Expr<Dynamic>());
        var job = Seq("VIA JOB", Expr<Source>(), "NUMBER", Expr<Source>());
        var exporting = Str("EXPORTING LIST TO MEMORY");
        var spool = Seq("SPOOL PARAMETERS", Expr<Source>());
        var archive = Seq("ARCHIVE PARAMETERS", Expr<Source>());
        var archiveMode = Seq("ARCHIVE MODE", Expr<Source>());
        var receiver = Seq("RECEIVER", Expr<Source>());
        var lineSize = Seq("LINE-SIZE", Expr<Source>());
        var lineCount = Seq("LINE-COUNT", Expr<Source>());
        var user = Seq("USER", Expr<Source>());
        var sset = Seq("USING SELECTION-SET", Expr<Source>());
        var ssetp = Seq("USING SELECTION-SETS OF PROGRAM", Expr<Source>());
        var uss = Seq("USING SELECTION-SCREEN", Expr<Source>());
        var free = Seq("WITH FREE SELECTIONS", Expr<Source>());
        var newList = Seq("NEW LIST IDENTIFICATION", Expr<Source>());
        var layout = Seq("LAYOUT", Expr<Source>());
        var cover = Seq("SAP COVER PAGE", Expr<Source>());
        var copies = Seq("COPIES", Expr<Source>());
        var datasetExpiration = Seq("DATASET EXPIRATION", Expr<Source>());
        var coverText = Seq("COVER TEXT", Expr<Source>());
        var listName = Seq("LIST NAME", Expr<Source>());

        var keep = Seq("KEEP IN SPOOL", Expr<Source>());
        var imm = Seq("IMMEDIATELY", Expr<Source>());
        var dest = Seq("DESTINATION", Expr<Source>());
        var language = Seq("LANGUAGE", Expr<Source>());

        var perm = Per(PlusPrio(awith),
                         selectionTable,
                         PlusPrio(awith),
                         PlusPrio(awith),
                         spool,
                         lineSize,
                         lineCount,
                         archive,
                         archiveMode,
                         receiver,
                         user,
                         sset,
                         ssetp,
                         keep,
                         cover,
                         imm,
                         layout,
                         dest,
                         coverText,
                         listName,
                         language,
                         free,
                         newList,
                         uss,
                         copies,
                         datasetExpiration,
                         "TO SAP-SPOOL",
                         "WITHOUT SPOOL DYNPRO",
                         "VIA SELECTION-SCREEN",
                         exporting,
                         Expr<AndReturn>(),
                         job);

        var ret = Seq("SUBMIT", prog, Opt(perm));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
