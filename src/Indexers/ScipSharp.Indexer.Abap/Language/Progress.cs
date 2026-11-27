// Ported from abaplint packages/core/src/progress.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap;

public interface IProgress
{
    void Set(int total, string text);
    Task Tick(string text);
    // todo, tickSync will be removed sometime
    void TickSync(string text);
}
