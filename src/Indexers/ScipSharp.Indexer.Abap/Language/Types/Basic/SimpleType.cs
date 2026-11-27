// Ported from abaplint packages/core/src/abap/types/basic/simple_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class SimpleType : AbstractType
{
    private static readonly SimpleType singleton = new SimpleType();

    public static SimpleType Get()
    {
        return SimpleType.singleton;
    }

    private SimpleType()
        : base()
    {
    }

    public override string ToText(int level)
    {
        return "```simple```";
    }

    public override string ToABAP()
    {
        return "simple";
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
        return "abap.TODO_SIMPLE";
    }
}
