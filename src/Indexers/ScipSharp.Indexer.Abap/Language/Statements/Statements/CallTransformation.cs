// Ported from abaplint packages/core/src/abap/2_statements/statements/call_transformation.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CallTransformation : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var options = Seq("OPTIONS", Expr<CallTransformationOptions>());

        var parameters = Seq("PARAMETERS", Expr<CallTransformationParameters>());
        var objects = Seq("OBJECTS", Expr<CallTransformationParameters>());

        var source2 = Seq("XML", Expr<SimpleSource3>());
        var source = Seq("SOURCE", Alt(Expr<CallTransformationParameters>(), source2));

        var result2 = Seq("XML", Expr<Target>());
        var result = Seq("RESULT", Alt(Expr<CallTransformationParameters>(), result2));

        var call = Seq("CALL TRANSFORMATION",
                         Alt(Expr<NamespaceSimpleName>(), Expr<Dynamic>()),
                         Per(options, parameters, objects, source, result));
        return VerNotLang(LanguageVersion.KeyUser, call);
    }
}
