// Ported from abaplint packages/core/src/abap/3_structures/structures/process_on_value_request.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class ProcessOnValueRequest : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var pov = Star(Sta<Statements.Field>());

        return Seq(Sta<Statements.ProcessOnValueRequest>(), pov);
    }
}
