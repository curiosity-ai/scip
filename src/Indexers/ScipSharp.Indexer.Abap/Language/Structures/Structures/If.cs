// Ported from abaplint packages/core/src/abap/3_structures/structures/if.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class If : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var contents = Seq(Opt(Sub<Body>()),
                             Star(Sub<ElseIf>()),
                             Opt(Sub<Else>()));

        return BeginEnd(Sta<Statements.If>(),
                        contents,
                        Sta<Statements.EndIf>());
    }
}
