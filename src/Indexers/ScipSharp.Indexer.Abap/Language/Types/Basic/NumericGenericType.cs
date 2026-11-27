// Ported from abaplint packages/core/src/abap/types/basic/numeric_generic_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class NumericGenericType : AbstractType
{
    public NumericGenericType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```NUMERIC```";
    }

    public override bool IsGeneric()
    {
        return true;
    }

    public override string ToABAP()
    {
        throw new Exception("NumericGenericType, generic");
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.TODO_NUMERICGENERIC";
    }
}
