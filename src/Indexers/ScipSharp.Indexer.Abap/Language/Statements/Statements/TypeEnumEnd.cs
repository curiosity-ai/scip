// Ported from abaplint packages/core/src/abap/2_statements/statements/type_enum_end.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using ScipSharp.Indexer.Abap.Statements.Expressions;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Statements;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class TypeEnumEnd : IStatement
{

    public IStatementRunnable GetMatcher()
    {
        var structure = Seq("STRUCTURE", Expr<NamespaceSimpleName>());

        var ret = Ver(Release.v751, Seq("TYPES", "END OF", "ENUM", Expr<NamespaceSimpleName>(), Opt(structure)), also: AlsoIn.OpenABAP);

        return ret;
    }
}
