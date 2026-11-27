// Ported from abaplint packages/core/src/abap/types/basic/xsequence_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class XSequenceType : AbstractType
{
    public XSequenceType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```xsequence```";
    }

    public override bool IsGeneric()
    {
        return true;
    }

    public override string ToABAP()
    {
        throw new Exception("xsequence, generic");
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.TODO_XSEQUENCE";
    }
}
