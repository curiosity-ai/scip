// Ported from abaplint packages/core/src/files/memory_file.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Files;

public class MemoryFile : AbstractFile
{
    private readonly string raw;

    public MemoryFile(string filename, string raw) : base(filename)
    {
        this.raw = raw;
    }

    public override string GetRaw() => raw;

    public override string[] GetRawRows() => raw.Split('\n');
}
