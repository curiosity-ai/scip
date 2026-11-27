// Ported from abaplint packages/core/src/abap/3_structures/structures/define.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Define : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.Define>(),
                        Star(Sta<Statements.MacroContent>()),
                        Sta<Statements.EndOfDefinition>());
    }
}
