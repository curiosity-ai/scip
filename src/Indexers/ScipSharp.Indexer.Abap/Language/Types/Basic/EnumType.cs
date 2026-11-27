// Ported from abaplint packages/core/src/abap/types/basic/enum_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class EnumType : AbstractType
{
    public EnumType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "enum";
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override string ToABAP()
    {
        return JsUtils.Or(this.GetQualifiedName(), "enum");
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.TODO_ENUM";
    }
}
