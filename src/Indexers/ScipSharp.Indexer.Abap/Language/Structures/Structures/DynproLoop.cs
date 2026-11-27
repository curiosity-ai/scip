// Ported from abaplint packages/core/src/abap/3_structures/structures/dynpro_loop.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class DynproLoop : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.DynproLoop>(),
                        Star(Alt(Sta<Statements.Module>(), Sta<Statements.Field>(), Sub<Chain>())),
                        Sta<Statements.EndLoop>());
    }
}
