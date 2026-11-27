// Ported from abaplint packages/core/src/abap/2_statements/expressions/sql_alias_field.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class SQLAliasField : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        var aliasField = Regex(@"^(\/\w+\/)?\w+~(\/\w+\/)?\w+$");
        var ctePrefixedAliasField = Seq(Tok<Tokens.WPlus>(), aliasField);
        return Seq(AltPrio(ctePrefixedAliasField, aliasField),
                   StarPrio(Seq(Tok<Tokens.Dash>(), Regex(@"^\w+$"))));
    }
}
