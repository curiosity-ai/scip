// Ported from abaplint packages/core/src/abap/3_structures/structures/catch.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Catch : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var cat = Seq(Sta<Statements.Catch>(), Opt(Sub<Body>()));

        return cat;
    }
}
