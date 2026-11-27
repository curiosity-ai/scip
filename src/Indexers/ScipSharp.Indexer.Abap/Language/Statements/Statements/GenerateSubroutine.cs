// Ported from abaplint packages/core/src/abap/2_statements/statements/generate_subroutine.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class GenerateSubroutine : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var name = Seq("NAME", Expr<Source>());
        var message = Seq("MESSAGE", Expr<Target>());
        var messageid = Seq("MESSAGE-ID", Expr<Target>());
        var line = Seq("LINE", Expr<Target>());
        var word = Seq("WORD", Expr<Target>());
        var offset = Seq("OFFSET", Expr<Target>());
        var @short = Seq("SHORTDUMP-ID", Expr<Target>());
        var include = Seq("INCLUDE", Expr<Target>());

        var ret = Seq("GENERATE SUBROUTINE POOL",
                        Expr<Source>(),
                        Per(name, message, line, word, include, offset, messageid, @short));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
