// Ported from abaplint packages/core/src/abap/3_structures/structures/select.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Select : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.SelectLoop>(),
                        Opt(Sub<Body>()),
                        Sta<Statements.EndSelect>());
    }
}
