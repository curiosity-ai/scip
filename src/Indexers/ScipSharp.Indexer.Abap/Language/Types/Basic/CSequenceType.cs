// Ported from abaplint packages/core/src/abap/types/basic/csequence_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class CSequenceType : AbstractType
{
    public CSequenceType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```csequence```";
    }

    public override bool IsGeneric()
    {
        return true;
    }

    public override string ToABAP()
    {
        throw new Exception("csequence, generic");
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.TODO_CSEQUENCE";
    }
}
