// Ported from abaplint packages/core/src/abap/3_structures/structures/loop_extract.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class LoopExtract : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.LoopExtract>(),
                        Star(Sub<Body>()),
                        Sta<Statements.EndLoop>());
    }
}
