// Ported from abaplint packages/core/src/abap/3_structures/structures/statics.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Statics : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.StaticBegin>(),
                        Star(Alt(Sta<Statements.Static>(), Sta<Statements.IncludeType>())),
                        Sta<Statements.StaticEnd>());
    }
}
