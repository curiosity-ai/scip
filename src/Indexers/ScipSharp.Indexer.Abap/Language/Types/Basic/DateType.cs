// Ported from abaplint packages/core/src/abap/types/basic/date_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class DateType : AbstractType
{
    public DateType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```d```";
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override string ToABAP()
    {
        return "d";
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.dats";
    }
}
