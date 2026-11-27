// Ported from abaplint packages/core/src/abap/5_syntax/assert_error.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Syntax;

public class AssertError : Exception
{
    public AssertError(string? message = null)
        : base(message)
    {
    }
}
