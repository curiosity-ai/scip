// Ported from abaplint packages/core/src/abap/2_statements/statements/create_object.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class CreateObject : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var exporting = Seq("EXPORTING", Expr<ParameterListS>());
        var exceptions = Seq("EXCEPTIONS", Expr<ParameterListExceptions>());
        var ptable = Seq("PARAMETER-TABLE", Expr<Source>());
        var etable = Seq("EXCEPTION-TABLE", Expr<Source>());
        var area = Seq("AREA HANDLE", Expr<Source>());
        var type = Seq("TYPE", AltPrio(Expr<ClassName>(), Expr<Dynamic>()));

        var ret = Seq("CREATE OBJECT",
                        Expr<Target>(),
                        OptPrio(Per(type, VerNotLang(LanguageVersion.Cloud, area))),
                        OptPrio(AltPrio(exporting, ptable)),
                        OptPrio(AltPrio(exceptions, etable)));

        return VerNotLang(LanguageVersion.KeyUser, ret);
    }
}
