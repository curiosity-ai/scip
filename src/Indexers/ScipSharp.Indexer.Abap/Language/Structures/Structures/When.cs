// Ported from abaplint packages/core/src/abap/3_structures/structures/when.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class When : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var when = Seq(Alt(Sta<Statements.When>(), Sta<Statements.WhenOthers>()), Opt(Sub<Body>()));

        return when;
    }
}
