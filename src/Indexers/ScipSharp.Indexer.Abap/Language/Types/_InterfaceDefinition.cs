// Ported from abaplint packages/core/src/abap/types/_interface_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;

namespace ScipSharp.Indexer.Abap.Types;

public class IImplementing
{
    public string Name { get; set; }
    public bool Partial { get; set; }
}

public interface IInterfaceDefinition
{
    // members of class Identifier, which the TS interface extends
    string GetName();
    bool Equals(global::ScipSharp.Indexer.Abap.FileInformation.Identifier id);
    AbstractToken GetToken();
    string GetFilename();
    Position GetStart();
    Position GetEnd();
    IAttributes GetAttributes();
    ITypeDefinitions GetTypeDefinitions();
    IMethodDefinitions GetMethodDefinitions();
    List<IEventDefinition> GetEvents();
    bool IsGlobal();
    string? GetSuperClass();
    List<Alias> GetAliases();
    List<IImplementing> GetImplementing();
}
