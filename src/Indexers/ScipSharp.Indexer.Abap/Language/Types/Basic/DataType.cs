// Ported from abaplint packages/core/src/abap/types/basic/data_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class DataType : AbstractType
{
    public DataType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```data```";
    }

    public override string ToABAP()
    {
        return "data";
    }

    public override bool IsGeneric()
    {
        return true;
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.TODO_DATA";
    }
}
