// Ported from abaplint packages/core/src/abap/3_structures/structures/form.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class Form : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return BeginEnd(Sta<Statements.Form>(),
                        Opt(Sub<Body>()),
                        Sta<Statements.EndForm>());
    }
}
