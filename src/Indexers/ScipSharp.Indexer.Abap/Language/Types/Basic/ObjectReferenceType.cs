// Ported from abaplint packages/core/src/abap/types/basic/object_reference_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;

namespace ScipSharp.Indexer.Abap.Types.Basic;

// use GenericObjectReferenceType for REF TO OBJECT
public class ObjectReferenceType : AbstractType
{
    private readonly Identifier identifier;

    public ObjectReferenceType(Identifier id, AbstractTypeData? extra = null)
        : base(extra)
    {
        this.identifier = id;
    }

    public string GetIdentifierName()
    {
        return this.identifier.GetName();
    }

    public override string ToText(int level)
    {
        return "```REF TO " + this.identifier.GetName() + "```";
    }

    public override string ToABAP()
    {
        return "REF TO " + this.identifier.GetName();
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public Identifier GetIdentifier()
    {
        return this.identifier;
    }

    public override string ToCDS()
    {
        return "abap.TODO_OBJECTREF";
    }
}
