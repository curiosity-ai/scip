// Ported from abaplint packages/core/src/abap/3_structures/structures/catch_system_exceptions.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class CatchSystemExceptions : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.CatchSystemExceptions>(),
                        Opt(Sub<Body>()),
                        Sta<Statements.EndCatch>());
    }
}
