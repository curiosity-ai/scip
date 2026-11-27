// Ported from abaplint packages/core/src/abap/2_statements/expressions/type_name.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Statements;
using static ScipSharp.Indexer.Abap.Statements.Combi;

namespace ScipSharp.Indexer.Abap.Statements.Expressions;

using Tokens = ScipSharp.Indexer.Abap.Lexer.Tokens;

// todo, can this be replaced with one of the FieldChain expressions?
public class TypeName : Expression
{

    public override IStatementRunnable GetRunnable()
    {
        var name = Regex(@"^[\w~\/%$]+$");
        var cla = Seq(name, Alt(Tok<Tokens.StaticArrow>(), Tok<Tokens.InstanceArrow>()));
        var field = Seq(Tok<Tokens.Dash>(), name);
        return Seq(Opt(cla), name, StarPrio(field));
    }
}
