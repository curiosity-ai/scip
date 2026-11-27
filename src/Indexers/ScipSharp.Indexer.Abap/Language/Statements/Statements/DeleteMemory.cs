// Ported from abaplint packages/core/src/abap/2_statements/statements/delete_memory.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class DeleteMemory : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var memory = Seq("MEMORY ID", Expr<Source>());

        var id = Seq("ID", Expr<Source>());
        var client = Seq("CLIENT", Expr<Source>());
        var shared = Seq("SHARED", AltPrio("MEMORY", "BUFFER"), Expr<Expressions.Field>(), "(", Regex(@"^[\w%]+$"), ")", OptPrio(client), id);

        var ret = Seq("DELETE FROM", Alt(memory, shared));

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
