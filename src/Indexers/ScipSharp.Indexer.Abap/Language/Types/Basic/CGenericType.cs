// Ported from abaplint packages/core/src/abap/types/basic/cgeneric_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class CGenericType : AbstractType
{
    private static readonly CGenericType singleton = new CGenericType();

    public static CGenericType Get()
    {
        return CGenericType.singleton;
    }

    private CGenericType()
        : base()
    {
    }

    public override string ToText(int level)
    {
        return "```c```";
    }

    public override bool IsGeneric()
    {
        return true;
    }

    public override string ToABAP()
    {
        throw new Exception("c, generic");
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.TODO_CGENERIC";
    }
}
