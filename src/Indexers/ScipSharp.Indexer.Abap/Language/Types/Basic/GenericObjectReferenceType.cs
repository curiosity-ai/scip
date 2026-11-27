// Ported from abaplint packages/core/src/abap/types/basic/generic_object_reference_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class GenericObjectReferenceType : AbstractType
{
    public GenericObjectReferenceType(AbstractTypeData? input = null)
        : base(input)
    {
    }


    public override string ToText(int level)
    {
        return "```REF TO object```";
    }

    public override bool IsGeneric()
    {
        // a DATA definition can be "REF TO object", so its not generic
        return false;
    }

    public override string ToABAP()
    {
        return "REF TO object";
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.TODO_GENERICOBJECTREF";
    }
}
