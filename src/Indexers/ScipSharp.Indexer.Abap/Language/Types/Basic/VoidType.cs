// Ported from abaplint packages/core/src/abap/types/basic/void_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class VoidType : AbstractType
{
    private static readonly Dictionary<string, VoidType> singletons = new Dictionary<string, VoidType>();

    public static VoidType Get(string? voided, string? qualifiedName = null)
    {
        lock (singletons)
        {
            var key = (voided ?? "\u0000") + "\u0001" + (qualifiedName ?? "\u0000");
            if (VoidType.singletons.ContainsKey(key))
            {
                return VoidType.singletons.GetValueOrDefault(key)!;
            }

            var ret = new VoidType(voided, qualifiedName);
            VoidType.singletons[key] = ret;
            return ret;
        }
    }

    // this contains the name of the type that was the original reason for the void
    private readonly string? voided;

    private VoidType(string? voided, string? qualifiedName = null)
        : base(new AbstractTypeData { QualifiedName = qualifiedName })
    {
        this.voided = voided;
    }

    public string? GetVoided()
    {
        return this.voided;
    }

    public override string ToABAP()
    {
        return JsUtils.Or(this.voided, "VOIDEDtoABAP");
    }

    public override string ToText(int level)
    {
        return "Void(" + JsUtils.Str(this.voided) + ")";
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override bool ContainsVoid()
    {
        return true;
    }

    public override string ToCDS()
    {
        return "abap.TODO_VOID";
    }
}
