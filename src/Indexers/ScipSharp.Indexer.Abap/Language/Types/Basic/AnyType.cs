// Ported from abaplint packages/core/src/abap/types/basic/any_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class AnyType : AbstractType
{
    private static readonly Dictionary<string, AnyType> singletons = new Dictionary<string, AnyType>();

    public static AnyType Get(AbstractTypeData? input = null)
    {
        lock (singletons)
        {
            var key = AbstractTypeData.KeyOf(input);
            if (AnyType.singletons.ContainsKey(key))
            {
                return AnyType.singletons.GetValueOrDefault(key)!;
            }

            var ret = new AnyType(input);
            AnyType.singletons[key] = ret;
            return ret;
        }
    }

    private AnyType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```any```";
    }

    public override string ToABAP()
    {
        return "any";
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
        return "abap.TODO_ANY";
    }
}
