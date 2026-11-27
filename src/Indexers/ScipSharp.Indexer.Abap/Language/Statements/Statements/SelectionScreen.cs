// Ported from abaplint packages/core/src/abap/2_statements/statements/selectionscreen.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SelectionScreen : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var text = AltPrio(Expr<TextElement>(), Expr<InlineField>());

        var beginBlock = Seq("BEGIN OF BLOCK",
                               Expr<BlockName>(),
                               OptPrio("WITH FRAME"),
                               OptPrio(Seq("TITLE", text)),
                               OptPrio("NO INTERVALS"));
        var endBlock = Seq("END OF BLOCK", Expr<BlockName>());

        var nesting = Seq("NESTING LEVEL", Expr<Source>());

        var scrOptions = Per(Seq("AS", Alt("WINDOW", "SUBSCREEN")),
                               Seq("TITLE", text),
                               "NO INTERVALS",
                               nesting);

        var beginScreen = Seq("BEGIN OF SCREEN",
                                Expr<Integer>(),
                                Opt(scrOptions));

        var endScreen = Seq("END OF SCREEN", Expr<Integer>());

        var beginLine = Str("BEGIN OF LINE");
        var endLine = Str("END OF LINE");

        var modif = Seq("MODIF ID", Expr<Modif>());

        var visible = Seq("VISIBLE LENGTH", Regex(@"^\d+$"));

        var ldbId = Seq("ID", Regex(@"^\w+$"));
        var ldb = Seq("FOR FIELD", Expr<FieldSub>(), OptPrio(ldbId));

        var commentOpt = Per(ldb, modif, visible);

        var position = Seq(Opt(Regex(@"^\/?[\d\w]+$")),
                             AltPrio(Tok<Tokens.ParenLeft>(), Tok<Tokens.WParenLeft>()),
                             Expr<Integer>(),
                             AltPrio(Tok<Tokens.ParenRightW>(), Tok<Tokens.ParenRight>()));

        var comment = Seq("COMMENT",
                            position,
                            Opt(text),
                            Opt(commentOpt));

        var command = Seq("USER-COMMAND", Alt(Expr<Expressions.Field>(), Expr<Expressions.Constant>()));

        var push = Seq("PUSHBUTTON",
                         position,
                         text,
                         command,
                         Opt(modif),
                         Opt(visible));

        var prog = Seq("PROGRAM", Expr<Expressions.Field>());
        var def = Seq("DEFAULT", Opt(prog), "SCREEN", Expr<Integer>());

        var tab = Seq("TAB",
                        Tok<Tokens.WParenLeft>(),
                        Expr<Integer>(),
                        Tok<Tokens.ParenRightW>(),
                        text,
                        command,
                        Opt(def),
                        Opt(modif));

        var func = Seq("FUNCTION KEY", Expr<Integer>());

        var skip = Seq("SKIP", Opt(Expr<Integer>()));

        var posSymbols = AltPrio("POS_LOW", "POS_HIGH");

        // number between 1 and 83
        var posIntegers = Regex(@"^(0?[1-9]|[1234567][0-9]|8[0-3])$");

        var pos = Seq("POSITION",
                        AltPrio(posIntegers, posSymbols),
                        Opt(Seq("FOR TABLE", Expr<Expressions.Field>())));

        var incl = Seq("INCLUDE BLOCKS", Expr<BlockName>());

        var tabbed = Seq("BEGIN OF TABBED BLOCK",
                           Expr<BlockName>(),
                           "FOR",
                           Expr<Integer>(),
                           "LINES",
                           OptPrio("NO INTERVALS"));

        var uline = Seq("ULINE", Opt(position), Opt(modif));

        var param = Seq("INCLUDE PARAMETERS", Expr<Expressions.Field>());
        var iso = Seq("INCLUDE SELECT-OPTIONS", Expr<Expressions.Field>());

        var exclude = Seq("EXCLUDE", Alt("IDS", "PARAMETERS"), Regex(@"^\w+$"));

        var beginVersion = Seq("BEGIN OF VERSION", Regex(@"^\w+$"), Expr<TextElement>());
        var endVersion = Seq("END OF VERSION", Regex(@"^\w+$"));

        var ret = Seq("SELECTION-SCREEN",
                        AltPrio(comment,
                                func,
                                skip,
                                pos,
                                incl,
                                iso,
                                push,
                                tab,
                                uline,
                                beginBlock,
                                tabbed,
                                endBlock,
                                beginLine,
                                endLine,
                                param,
                                beginScreen,
                                endScreen,
                                exclude,
                                beginVersion,
                                endVersion,
                                def));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
