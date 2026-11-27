// Ported from abaplint packages/core/src/abap/2_statements/statement_runnable.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Statements;

public interface IStatementRunnable
{
    /// <summary>
    /// Runs the matcher on every input. The input list is never mutated, the returned
    /// list may be the input itself or a shared empty list and must not be mutated either.
    /// </summary>
    IReadOnlyList<Result> Run(IReadOnlyList<Result> r);
    string Railroad();
    string ToStr();
    List<string> GetUsing();
    List<string> ListKeywords();
    /// <summary>Return first keyword, blank if not applicable.</summary>
    List<string> First();
}
