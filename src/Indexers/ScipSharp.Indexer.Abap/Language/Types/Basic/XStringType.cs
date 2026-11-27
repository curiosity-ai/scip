// Ported from abaplint packages/core/src/abap/types/basic/xstring_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class XStringType : AbstractType
{
    private static readonly Dictionary<string, XStringType> singletons = new Dictionary<string, XStringType>();

    public static XStringType Get(AbstractTypeData? input = null)
    {
        lock (singletons)
        {
            var key = AbstractTypeData.KeyOf(input);
            if (XStringType.singletons.ContainsKey(key))
            {
                return XStringType.singletons.GetValueOrDefault(key)!;
            }

            var ret = new XStringType(input);
            XStringType.singletons[key] = ret;
            return ret;
        }
    }

    private XStringType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    public override string ToText(int level)
    {
        return "```xstring```";
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override string ToABAP()
    {
        return "xstring";
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.rawstring";
    }
}
