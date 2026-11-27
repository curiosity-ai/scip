// Ported from abaplint packages/core/src/abap/3_structures/structures/process_before_output.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class ProcessBeforeOutput : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        var pbo = Star(Alt(
          Sta<Statements.Module>(),
          Sta<Statements.Field>(),
          Sta<Statements.CallSubscreen>(),
          Sub<LoopExtract>(),
          Sub<DynproLoop>()));

        return Seq(Sta<Statements.ProcessBeforeOutput>(), pbo);
    }
}
