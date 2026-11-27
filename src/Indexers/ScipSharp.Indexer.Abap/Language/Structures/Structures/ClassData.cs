// Ported from abaplint packages/core/src/abap/3_structures/structures/class_data.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class ClassData : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.ClassDataBegin>(),
                        Star(Alt(Sta<Statements.ClassData>(), Sub<ClassData>())),
                        Sta<Statements.ClassDataEnd>());
    }
}
