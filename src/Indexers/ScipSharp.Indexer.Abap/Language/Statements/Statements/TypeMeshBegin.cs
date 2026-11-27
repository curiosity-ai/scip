// Ported from abaplint packages/core/src/abap/2_statements/statements/type_mesh_begin.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TypeMeshBegin : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var ret = Ver(Release.v740sp05, Seq("TYPES", "BEGIN OF MESH", Expr<NamespaceSimpleName>()));
        return ret;
    }
}
