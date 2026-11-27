// Ported from abaplint packages/core/src/abap/types/basic/ngeneric_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

// "TYPE n" of a method or FORM parameter, where no length can be given.
// It is a NumericType, so everything that handles n keeps treating it as such
public class NGenericType : NumericType
{
    private static readonly NGenericType singleton = new NGenericType();

    public static NGenericType Get()
    {
        return NGenericType.singleton;
    }

    private NGenericType()
        : base(1)
    {
    }

    public override string ToText(int level)
    {
        return "```n```";
    }

    public override bool IsGeneric()
    {
        return true;
    }
}
