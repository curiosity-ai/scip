// Ported from abaplint packages/core/src/abap/3_structures/structures/constants.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Constants : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.ConstantBegin>(),
                        Star(Alt(Sta<Statements.Constant>(),
                                 Sta<Statements.Include>(),
                                 Sub<Constants>())),
                        Sta<Statements.ConstantEnd>());
    }
}
