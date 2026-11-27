// Ported from abaplint packages/core/src/abap/2_statements/statements/infotypes.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class Infotypes : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var occurs = Seq("OCCURS", Expr<Expressions.Constant>());
        var name = Seq("NAME", Expr<FieldSub>());
        string mode = "MODE N";
        var valid = Seq("VALID FROM", Expr<Source>(), "TO", Expr<Source>());

        var ret = Seq("INFOTYPES", Regex(@"^\d\d\d\d$"), OptPrio(valid), OptPrio(name), OptPrio(occurs), OptPrio(mode));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
