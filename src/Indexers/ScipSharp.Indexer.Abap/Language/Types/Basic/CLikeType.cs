// Ported from abaplint packages/core/src/abap/types/basic/clike_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class CLikeType : AbstractType
{
    private static readonly CLikeType singleton = new CLikeType();

    public static CLikeType Get()
    {
        return CLikeType.singleton;
    }

    private CLikeType()
        : base()
    {
    }

    public override string ToText(int level)
    {
        return "```clike```";
    }

    public override bool IsGeneric()
    {
        return true;
    }

    public override string ToABAP()
    {
        throw new Exception("clike, generic");
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.TODO_CLIKE";
    }
}
