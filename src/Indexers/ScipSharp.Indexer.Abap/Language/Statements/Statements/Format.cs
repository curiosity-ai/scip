// Ported from abaplint packages/core/src/abap/2_statements/statements/format.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Format : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var eq = Seq("=", Expr<Source>());
        var value = AltPrio(eq, "ON", "OFF");

        var options = Per("RESET",
                            Seq("INTENSIFIED", Opt(value)),
                            Seq("INVERSE", Opt(value)),
                            Seq("HOTSPOT", Opt(value)),
                            Seq("FRAMES", value),
                            Seq("INPUT", Opt(value)),
                            Expr<Color>());

        var ret = Seq("FORMAT", options);

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
