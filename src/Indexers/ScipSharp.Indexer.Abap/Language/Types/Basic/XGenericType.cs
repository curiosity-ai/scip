// Ported from abaplint packages/core/src/abap/types/basic/xgeneric_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class XGenericType : AbstractType
{
    private static readonly XGenericType singleton = new XGenericType();

    public static XGenericType Get()
    {
        return XGenericType.singleton;
    }

    private XGenericType()
        : base()
    {
    }

    public override string ToText(int level)
    {
        return "```x```";
    }

    public override bool IsGeneric()
    {
        return true;
    }

    public override string ToABAP()
    {
        throw new Exception("x, generic");
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
