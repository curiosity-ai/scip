// Ported from abaplint packages/core/src/abap/3_structures/structures/chain.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Chain : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.Chain>(),
                        Star(Alt(Sta<Statements.Field>(), Sta<Statements.Module>())),
                        Sta<Statements.EndChain>());
    }
}
