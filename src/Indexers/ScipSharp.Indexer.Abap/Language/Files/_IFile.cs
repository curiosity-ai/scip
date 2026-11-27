// Ported from abaplint packages/core/src/files/_ifile.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Files;

public interface IFile
{
    string GetFilename();
    string? GetObjectType();
    string GetObjectName();
    string GetRaw();
    string[] GetRawRows();
}
