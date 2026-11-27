// Ported from abaplint packages/core/src/abap/types/basic/float_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

// this is the ABAP "F" type, which is IEEE?
// todo, same as FloatingPointType ?
public class FloatType : AbstractType
{
    public FloatType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```f```";
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override string ToABAP()
    {
        return "f";
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
