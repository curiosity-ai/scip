// Ported from abaplint packages/core/src/abap/2_statements/statements/type_mesh.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TypeMesh : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var on = Seq("ON", Expr<NamespaceSimpleName>(), "=", Expr<NamespaceSimpleName>(), Star(Seq("AND", Expr<NamespaceSimpleName>(), "=", Expr<NamespaceSimpleName>())));

        var @using = Seq("USING KEY", Expr<NamespaceSimpleName>());

        var association = Seq("ASSOCIATION", Expr<NamespaceSimpleName>(), "TO", Expr<NamespaceSimpleName>(), Plus(on));

        var ret = Ver(Release.v740sp05, Seq("TYPES", Expr<NamespaceSimpleName>(), "TYPE", Opt("REF TO"), Expr<TypeName>(), Plus(association), Opt(@using)));

        return ret;
    }
}
