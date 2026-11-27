// Ported from abaplint packages/core/src/abap/2_statements/statements/module.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Module : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var sw = Seq("SWITCH", Expr<NamespaceSimpleName>());

        var ret = Seq("MODULE",
                        Expr<FormName>(),
                        Opt(Alt("INPUT", "OUTPUT", "ON CHAIN-REQUEST", "ON CHAIN-INPUT", "AT EXIT-COMMAND", sw)));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
