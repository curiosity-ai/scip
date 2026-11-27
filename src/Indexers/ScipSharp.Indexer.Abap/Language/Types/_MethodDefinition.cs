// Ported from abaplint packages/core/src/abap/types/_method_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap.Types;

public interface IMethodDefinition
{
    // members of class Identifier, which the TS interface extends
    string GetName();
    bool Equals(global::ScipSharp.Indexer.Abap.FileInformation.Identifier id);
    AbstractToken GetToken();
    string GetFilename();
    Position GetStart();
    Position GetEnd();
    Visibility GetVisibility();
    bool IsRedefinition();
    bool IsAbstract();
    bool IsStatic();
    bool IsEventHandler();
    string? GetEventName();
    string? GetEventClass();
    IMethodParameters GetParameters();
    List<string> GetRaising();
    List<string> GetExceptions();
}
