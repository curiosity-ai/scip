// Ported from abaplint packages/core/src/abap/3_structures/structures/type_enum.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class TypeEnum : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.TypeEnumBegin>(),
                        Star(Alt(Sta<Statements.TypeEnum>(), Sta<Statements.Type>())),
                        Sta<Statements.TypeEnumEnd>());
    }
}
