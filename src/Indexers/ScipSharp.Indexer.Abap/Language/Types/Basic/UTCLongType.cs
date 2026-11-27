// Ported from abaplint packages/core/src/abap/types/basic/utc_long_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class UTCLongType : AbstractType
{
    public UTCLongType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```utclong```";
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override string ToABAP()
    {
        return "utclong";
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.utcl";
    }
}
