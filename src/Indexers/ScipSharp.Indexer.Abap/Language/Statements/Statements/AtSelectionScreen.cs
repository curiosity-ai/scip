// Ported from abaplint packages/core/src/abap/2_statements/statements/at_selection_screen.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class AtSelectionScreen : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        string output = "OUTPUT";

        var value = Seq("ON VALUE-REQUEST FOR", Expr<FieldSub>());

        string exit = "ON EXIT-COMMAND";

        var field = Seq("ON", Expr<FieldSub>());

        var end = Seq("ON END OF", Expr<Expressions.Field>());

        var radio = Seq("ON RADIOBUTTON GROUP", Expr<Expressions.Field>());

        var block = Seq("ON BLOCK", Expr<BlockName>());

        var help = Seq("ON HELP-REQUEST FOR", Expr<FieldSub>());

        var ret = Seq("AT SELECTION-SCREEN",
                        Opt(Alt(output, value, radio, exit, field, end, help, block)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
