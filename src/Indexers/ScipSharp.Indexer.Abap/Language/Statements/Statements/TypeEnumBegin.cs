// Ported from abaplint packages/core/src/abap/2_statements/statements/type_enum_begin.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TypeEnumBegin : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var structure = Seq("STRUCTURE", Expr<NamespaceSimpleName>());

        var @base = Seq("BASE TYPE", Expr<NamespaceSimpleName>());

        var em = Seq("ENUM", Expr<NamespaceSimpleName>(), Opt(structure), Opt(@base));

        var ret = Ver(Release.v751, Seq("TYPES", "BEGIN OF", em), also: AlsoIn.OpenABAP);

        return ret;
    }
}
