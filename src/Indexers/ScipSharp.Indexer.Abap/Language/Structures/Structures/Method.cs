// Ported from abaplint packages/core/src/abap/3_structures/structures/method.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Method : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.MethodImplementation>(),
                        Opt(Alt(Sub<Body>(), Star(Sta<Statements.NativeSQL>()))),
                        Sta<Statements.EndMethod>());
    }
}
