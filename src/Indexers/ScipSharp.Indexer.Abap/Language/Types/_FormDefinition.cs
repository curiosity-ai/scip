// Ported from abaplint packages/core/src/abap/types/_form_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap.Types;

public interface IFormDefinition
{
    // members of class Identifier, which the TS interface extends
    string GetName();
    bool Equals(global::ScipSharp.Indexer.Abap.FileInformation.Identifier id);
    AbstractToken GetToken();
    string GetFilename();
    Position GetStart();
    Position GetEnd();

    List<TypedIdentifier> GetTablesParameters();
    List<TypedIdentifier> GetUsingParameters();
    List<TypedIdentifier> GetChangingParameters();
}
