// Ported from abaplint packages/core/src/abap/types/basic/pgeneric_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class PGenericType : AbstractType
{
    private static readonly PGenericType singleton = new PGenericType();

    public static PGenericType Get()
    {
        return PGenericType.singleton;
    }

    private PGenericType()
        : base()
    {
    }

    public override string ToText(int level)
    {
        return "```p```";
    }

    public override bool IsGeneric()
    {
        return true;
    }

    public override string ToABAP()
    {
        throw new Exception("p, generic");
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.TODO_PGENERIC";
    }
}
