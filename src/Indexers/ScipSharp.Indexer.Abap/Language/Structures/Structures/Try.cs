// Ported from abaplint packages/core/src/abap/3_structures/structures/try.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Try : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var block = Seq(Opt(Sub<Body>()), Star(Sub<Catch>()), Opt(Sub<Cleanup>()));

        return BeginEnd(Sta<Statements.Try>(),
                        block,
                        Sta<Statements.EndTry>());
    }
}
