// Ported from abaplint packages/core/src/abap/3_structures/structures/types.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Types : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.TypeBegin>(),
                        Star(Alt(Sta<Statements.Type>(),
                                 Sub<Types>(),
                                 Sta<Statements.MacroCall>(),
                                 Sta<Statements.Include>(),
                                 Sta<Statements.IncludeType>())),
                        Sta<Statements.TypeEnd>());
    }
}
