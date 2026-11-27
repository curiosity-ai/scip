// Ported from abaplint packages/core/src/abap/types/basic/string_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class StringType : AbstractType
{
    private static readonly Dictionary<string, StringType> singletons = new Dictionary<string, StringType>();

    public static StringType Get(AbstractTypeData? input = null)
    {
        lock (singletons)
        {
            var key = AbstractTypeData.KeyOf(input);
            if (StringType.singletons.ContainsKey(key))
            {
                return StringType.singletons.GetValueOrDefault(key)!;
            }

            var ret = new StringType(input);
            StringType.singletons[key] = ret;
            return ret;
        }
    }

    private StringType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    /** fully qualified symbolic name of the type */
    public override string? GetQualifiedName()
    {
        var qual = this.data?.QualifiedName;
        if (qual == null)
        {
            return "STRING";
        }
        return qual;
    }

    public override string ToText(int level)
    {
        return "```string```";
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override string ToABAP()
    {
        return "string";
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.string";
    }
}
