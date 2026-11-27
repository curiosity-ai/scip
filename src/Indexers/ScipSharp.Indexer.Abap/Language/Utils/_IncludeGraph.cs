// Ported from abaplint packages/core/src/utils/_include_graph.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Utils;

public interface IIncludeGraph
{
    List<string> ListMainForInclude(string filename);
    List<Issue> GetIssuesFile(IFile file);
}
