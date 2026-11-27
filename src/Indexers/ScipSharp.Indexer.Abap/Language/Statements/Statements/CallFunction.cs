// Ported from abaplint packages/core/src/abap/2_statements/statements/call_function.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallFunction : IStatement
{

    public IStatementRunnable GetMatcher()
    {

        var starting = VerNotLang(LanguageVersion.Cloud, Seq("STARTING NEW TASK", Expr<SimpleSource2>()));
        var update = VerNotLang(LanguageVersion.Cloud, Str("IN UPDATE TASK"));
        var unit = Seq("UNIT", Expr<Source>());
        var background = VerNotLang(LanguageVersion.Cloud, Seq("IN BACKGROUND", AltPrio("TASK", unit)));
        var calling = Seq("CALLING", Expr<MethodSource>(), "ON END OF TASK");
        var performing = Seq("PERFORMING", Expr<FormName>(), "ON END OF TASK");
        var separate = Str("AS SEPARATE UNIT");
        var keeping = Str("KEEPING LOGICAL UNIT OF WORK");

        var options = Per(starting, update, background, Expr<Destination>(), calling, performing, separate, keeping);

        var ex = Seq("EXCEPTION-TABLE", Expr<Source>());
        var dynamic = Alt(Seq("PARAMETER-TABLE", Expr<Source>(), Opt(ex)), ex);

        var call = Seq("CALL",
                         AltPrio("FUNCTION", VerNotLang(LanguageVersion.Cloud, "CUSTOMER-FUNCTION")),
                         Expr<FunctionName>(),
                         Opt(options),
                         Alt(Expr<FunctionParameters>(), dynamic));

        return VerNotLang(LanguageVersion.KeyUser, call);
    }
}
