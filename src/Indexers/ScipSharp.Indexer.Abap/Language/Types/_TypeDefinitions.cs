// Ported from abaplint packages/core/src/abap/types/_type_definitions.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;

namespace ScipSharp.Indexer.Abap.Types;

public class TypeDefinitionsEntry
{
    public TypedIdentifier Type { get; set; }
    public Visibility Visibility { get; set; }
}

public interface ITypeDefinitions
{

    List<TypeDefinitionsEntry> GetAll();
    TypedIdentifier? GetByName(string name);
}
