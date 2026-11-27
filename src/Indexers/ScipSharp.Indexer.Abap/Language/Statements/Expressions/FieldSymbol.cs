// Ported from abaplint packages/core/src/abap/2_statements/expressions/field_symbol.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

public class FieldSymbol : Expression
{
    public override IStatementRunnable GetRunnable()
    {
        // todo, this only allows one dash in the name
        var dashes = Seq(Regex(@"^<[\w\/%$\*]+$"), Tok<Tokens.Dash>(), Regex(@"^[\w\/%$\*]+>$"));

        return AltPrio(Regex(@"^<[\w\/%$\*\?]+>$"), dashes);
    }
}
