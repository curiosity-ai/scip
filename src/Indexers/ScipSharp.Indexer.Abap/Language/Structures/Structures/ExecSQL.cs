// Ported from abaplint packages/core/src/abap/3_structures/structures/exec_sql.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class ExecSQL : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.ExecSQL>(),
                        Star(Sta<Statements.NativeSQL>()),
                        Sta<Statements.EndExec>());
    }
}
