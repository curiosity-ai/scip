// Ported from abaplint packages/core/src/abap/2_statements/expressions/method_def_exceptions.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class MethodDefExceptions : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // parameter section keywords cannot be exception names
        var name = Seq(StopBefore1("IMPORTING", "EXPORTING", "CHANGING", "RETURNING", "RAISING", "EXCEPTIONS"), Expr<NamespaceSimpleName>());

        var exceptions = Seq("EXCEPTIONS", PlusPrio(name));

        return exceptions;
    }
}
