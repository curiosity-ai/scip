// Ported from abaplint packages/core/src/utils/excludeHelper.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap.Utils;

public class ExcludeHelper
{

    public static bool IsExcluded(string filename, List<Regex> excludePatterns)
    {
        foreach (var exclude in excludePatterns)
        {
            if ((JsUtils.Exec(exclude, filename)) != null)
            {
                return true;
            }
        }

        return false;
    }
}
