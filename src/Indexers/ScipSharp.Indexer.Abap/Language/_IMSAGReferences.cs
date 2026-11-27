// Ported from abaplint packages/core/src/_imsag_references.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Objects;

namespace ScipSharp.Indexer.Abap;

public interface IMSAGReferences
{
    void Clear(IObject obj);

    void AddUsing(string filename, AbstractToken token, string messageClass, string number);

    List<(AbstractToken token, string messageClass, string number)> ListByFilename(string filename);
    List<(string filename, AbstractToken token)> ListByMessage(string messageClass, string number);
}
