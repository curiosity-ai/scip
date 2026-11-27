// Ported from abaplint packages/core/src/abap/3_structures/structures/class_implementation.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class ClassImplementation : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        // the DEFINE statement is allowed between local method implementations, but not global?
        var body = Star(Alt(Sub<Define>(), Sta<Statements.Include>(), Sub<Method>()));

        return BeginEnd(Sta<Statements.ClassImplementation>(),
                        body,
                        Sta<Statements.EndClass>());
    }
}
