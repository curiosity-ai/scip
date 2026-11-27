// Ported from abaplint packages/core/src/abap/2_statements/statements/type_pools.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

// type pool usage
public class TypePools : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var fieldName = Regex(@"^\w+$");

        var ret = Seq("TYPE-POOLS", fieldName);

        return VerNotLang(LanguageVersion.Cloud, ret);
    }
}
