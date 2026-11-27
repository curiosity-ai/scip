// Ported from abaplint packages/core/src/abap/3_structures/structures/when_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class WhenType : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var when = Seq(Alt(Sta<Statements.WhenType>(), Sta<Statements.WhenOthers>()), Opt(Sub<Body>()));

        return when;
    }
}
