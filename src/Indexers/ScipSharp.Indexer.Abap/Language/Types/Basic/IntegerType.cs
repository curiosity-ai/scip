// Ported from abaplint packages/core/src/abap/types/basic/integer_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class IntegerType : AbstractType
{
    private static readonly Dictionary<string, IntegerType> singletons = new Dictionary<string, IntegerType>();

    public static IntegerType Get(AbstractTypeData? input = null)
    {
        lock (singletons)
        {
            var key = AbstractTypeData.KeyOf(input);
            if (IntegerType.singletons.ContainsKey(key))
            {
                return IntegerType.singletons.GetValueOrDefault(key)!;
            }

            var ret = new IntegerType(input);
            IntegerType.singletons[key] = ret;
            return ret;
        }
    }

    private IntegerType(AbstractTypeData? input = null)
        : base(input)
    {
    }

    /** fully qualified symbolic name of the type */
    public override string? GetQualifiedName()
    {
        var qual = this.data?.QualifiedName;
        if (qual == null)
        {
            return "I";
        }
        return qual;
    }

    public override string ToText(int level)
    {
        return "```i```";
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override string ToABAP()
    {
        return "i";
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public override string ToCDS()
    {
        return "abap.int4";
    }
}
