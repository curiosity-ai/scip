// Ported from abaplint packages/core/src/abap/3_structures/structures/case.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Case : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.Case>(),
                        Seq(Star(Sub<Normal>()), Star(Alt(Sub<When>(), Sta<Statements.MacroCall>(), Sta<Statements.Include>()))),
                        Sta<Statements.EndCase>());
    }
}
