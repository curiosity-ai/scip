// Ported from abaplint packages/core/src/abap/2_statements/statements/open_dataset.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class OpenDataset : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var mode = Seq("IN",
                         Opt("LEGACY"),
                         AltPrio("BINARY MODE", "TEXT MODE"));

        var code = Seq("CODE PAGE", Expr<Source>());

        var direction = Seq("FOR", AltPrio("OUTPUT", "INPUT", "UPDATE", "APPENDING"));
        var encoding = Seq("ENCODING", AltPrio("DEFAULT", "UTF-8", "NON-UNICODE"));
        var pos = Seq("AT POSITION", Expr<Source>());
        var message = Seq("MESSAGE", Expr<Target>());
        var ignoring = Str("IGNORING CONVERSION ERRORS");
        var replacement = Seq("REPLACEMENT CHARACTER", Expr<Source>());
        var bom = Str("SKIPPING BYTE-ORDER MARK");
        var wbom = Str("WITH BYTE-ORDER MARK");
        var type = Seq("TYPE", Expr<Source>());
        var filter = Seq("FILTER", Expr<Source>());
        var linetype = AltPrio("SMART", "NATIVE", "UNIX");
        var feed = Seq("WITH", linetype, "LINEFEED");
        var windows = Str("WITH WINDOWS LINEFEED");

        var ret = Seq("OPEN DATASET",
                        Expr<Source>(),
                        Opt(Per(direction, type, mode, wbom, replacement, filter, encoding, pos, message, ignoring, bom, code, feed, windows)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
