// Ported from abaplint packages/core/src/abap/2_statements/statements/type_pool.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

// type pool definition
public class TypePool : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var fieldName = Regex(@"^\w+$");

        var ret = Seq("TYPE-POOL", fieldName);

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
