// Ported from abaplint packages/core/src/abap/types/basic/decfloat_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class DecFloatType : AbstractType
{
    public DecFloatType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```decfloat```";
    }

    public override bool IsGeneric()
    {
        return true;
    }

    public override string ToABAP()
    {
        return "decfloat";
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.fltp";
    }
}
