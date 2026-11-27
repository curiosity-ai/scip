// Ported from abaplint packages/core/src/_iddic_references.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap;

public class IObjectAndToken
{
    public IObject? Object { get; set; }
    public AbstractToken? Token { get; set; }
    public string? Filename { get; set; }
}

public interface IDDICReferences
{
    void SetUsing(IObject obj, List<IObjectAndToken> @using);
    void AddUsing(IObject obj, IObjectAndToken? @using);
    void Clear(IObject obj);
    List<IObjectAndToken> ListUsing(IObject obj);
    List<IObjectAndToken> ListByFilename(string filename, int line);
    List<(string type, string name, AbstractToken? token, string? filename)> ListWhereUsed(IObject obj);
}
