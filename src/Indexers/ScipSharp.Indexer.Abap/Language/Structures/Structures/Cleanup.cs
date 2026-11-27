// Ported from abaplint packages/core/src/abap/3_structures/structures/cleanup.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Cleanup : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var cleanup = Seq(Sta<Statements.Cleanup>(), Opt(Sub<Body>()));
        return cleanup;
    }
}
