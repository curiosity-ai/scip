// Ported from abaplint packages/core/src/abap/types/_class_attributes.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;

namespace ScipSharp.Indexer.Abap.Types;

public interface IAttributes
{
    List<ClassAttribute> GetStatic();
    List<ClassAttribute> GetStaticsByVisibility(Visibility visibility);
    List<ClassAttribute> GetInstance();
    List<ClassAttribute> GetInstancesByVisibility(Visibility visibility);
    List<ClassConstant> GetConstantsByVisibility(Visibility visibility);
    TypedIdentifier? FindByName(string name);

    List<ClassConstant> GetConstants();
    List<ClassAttribute> GetAll();
    List<Alias> GetAliases();
    ITypeDefinitions GetTypes();
}
