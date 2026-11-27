// Ported from abaplint packages/core/src/abap/2_statements/statements/syntax_check.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SyntaxCheck : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var program = Seq("PROGRAM", Expr<Source>());
        var offset = Seq("OFFSET", Expr<Target>());
        var frame = Seq("FRAME ENTRY", Expr<Target>());
        var include = Seq("INCLUDE", Expr<Target>());
        var trace = Seq("TRACE-TABLE", Expr<Target>());
        var line = Seq("LINE", Expr<Target>());
        var word = Seq("WORD", Expr<Target>());
        var messageId = Seq("MESSAGE-ID", Expr<Target>());
        var message = Seq("MESSAGE", Expr<Target>());
        var id = Seq("ID", Expr<Source>(), "TABLE", Expr<Target>());
        var replacing = Seq("REPLACING", Expr<Target>());
        var directory = Seq("DIRECTORY ENTRY", Expr<Source>());
        var dump = Seq("SHORTDUMP-ID", Expr<Source>());
        var filter = Seq("FILTER", Expr<Source>());

        var syntax = Seq(OptPrio("PROGRAM"),
                           Expr<Source>(),
                           Per(message,
                               line,
                               word,
                               offset,
                               program,
                               replacing,
                               directory,
                               frame,
                               include,
                               messageId,
                               trace,
                               dump,
                               filter,
                               Plus(id)));

        var dynpro = Seq("DYNPRO",
                           Expr<Source>(),
                           Expr<Source>(),
                           Expr<Source>(),
                           Expr<Source>(),
                           Per(message, line, word, offset, messageId, trace));

        var ret = Seq("SYNTAX-CHECK FOR", Alt(syntax, dynpro));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
