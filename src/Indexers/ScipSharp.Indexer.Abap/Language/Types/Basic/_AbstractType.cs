// Ported from abaplint packages/core/src/abap/types/basic/_abstract_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class AbstractTypeData
{
    public string? QualifiedName { get; set; }
    public string? ConversionExit { get; set; }
    public bool? DerivedFromConstant { get; set; }
    public string? DdicName { get; set; }
    public string? RTTIName { get; set; }
    public string? Description { get; set; }

    /// <summary>Replaces JSON.stringify(input), used as the key of the type singletons.</summary>
    public static string KeyOf(AbstractTypeData? d)
    {
        if (d == null)
        {
            return "undefined";
        }
        static string S(object? o) => o == null ? "\u0000" : o.ToString()!;
        return string.Join("\u0001", S(d.QualifiedName), S(d.ConversionExit), S(d.DerivedFromConstant), S(d.DdicName), S(d.RTTIName), S(d.Description));
    }
}

public abstract class AbstractType
{
    protected readonly AbstractTypeData? data;

    public AbstractType(AbstractTypeData? input = null)
    {
        this.data = input;
    }

    public AbstractTypeData? GetAbstractTypeData()
    {
        return this.data;
    }

    /** fully qualified symbolic name of the type */
    public virtual string? GetQualifiedName()
    {
        return this.data?.QualifiedName;
    }

    public string? GetRTTIName()
    {
        return this.data?.RTTIName;
    }

    public string? GetDescription()
    {
        return this.data?.Description;
    }

    public string? GetConversionExit()
    {
        return this.data?.ConversionExit;
    }

    public string? GetDDICName()
    {
        return this.data?.DdicName;
    }

    public abstract string ToText(int level);
    public abstract string ToABAP();
    public abstract string ToCDS();
    public abstract bool IsGeneric();
    public abstract bool ContainsVoid();
}
