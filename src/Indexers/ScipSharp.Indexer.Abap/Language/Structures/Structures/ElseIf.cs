// Ported from abaplint packages/core/src/abap/3_structures/structures/elseif.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class ElseIf : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var body = Opt(Sub<Body>());
        var elseif = Seq(Sta<Statements.ElseIf>(), body);
        return elseif;
    }
}
