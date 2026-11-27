// Ported from abaplint packages/core/src/abap/3_structures/structures/body.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Body : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        // todo, this should be a "plus" instead, however its not implemented yet
        return Star(Sub<Normal>());
    }
}
