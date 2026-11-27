// Ported from abaplint packages/core/src/abap/3_structures/structures/interface_global.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class InterfaceGlobal : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return Seq(Star(Sta<Statements.TypePools>()),
                   Star(Sta<Statements.InterfaceLoad>()),
                   Sub<Interface>());
    }
}
