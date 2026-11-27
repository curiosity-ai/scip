// Ported from abaplint packages/core/src/abap/types/basic/integer8_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class Integer8Type : AbstractType
{
    public Integer8Type(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```int8```";
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override string ToABAP()
    {
        return "int8";
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.int8";
    }
}
