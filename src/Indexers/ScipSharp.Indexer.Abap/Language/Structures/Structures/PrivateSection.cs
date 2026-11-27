// Ported from abaplint packages/core/src/abap/3_structures/structures/private_section.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using static ScipSharp.Indexer.Abap.Structures.Structures.Combi;

namespace ScipSharp.Indexer.Abap.Structures.Structures;

using Statements = ScipSharp.Indexer.Abap.Statements.Statements;

public class PrivateSection : IStructure
{
    public IStructureRunnable GetMatcher()
    {
        return Seq(Sta<Statements.Private>(), Opt(Sub<SectionContents>()));
    }
}
