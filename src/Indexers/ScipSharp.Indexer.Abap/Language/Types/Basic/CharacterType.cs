// Ported from abaplint packages/core/src/abap/types/basic/character_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class CharacterType : AbstractType
{
    private readonly int length;

    public CharacterType(int length, AbstractTypeData? extra = null)
        : base(extra)
    {
        if (length <= 0)
        {
            throw new Exception("Bad LENGTH, Character");
        }
        this.length = length;
    }

    public CharacterType CloneType((string? qualifiedName, string? ddicName, bool? derivedFromConstant) input)
    {
        var clone = new AbstractTypeData { QualifiedName = this.GetAbstractTypeData()?.QualifiedName, ConversionExit = this.GetAbstractTypeData()?.ConversionExit, DerivedFromConstant = this.GetAbstractTypeData()?.DerivedFromConstant, DdicName = this.GetAbstractTypeData()?.DdicName, RTTIName = this.GetAbstractTypeData()?.RTTIName, Description = this.GetAbstractTypeData()?.Description };
        if (!string.IsNullOrEmpty(input.qualifiedName))
        {
            clone.QualifiedName = input.qualifiedName;
        }
        if (!string.IsNullOrEmpty(input.ddicName))
        {
            clone.DdicName = input.ddicName;
        }
        if (input.derivedFromConstant == true)
        {
            clone.DerivedFromConstant = input.derivedFromConstant.Value;
        }
        return new CharacterType(this.length, clone);
    }

    public int GetLength()
    {
        return this.length;
    }

    public override string ToText(int level)
    {
        return "```c LENGTH " + this.GetLength() + "```";
    }

    public override string ToABAP()
    {
        return "c LENGTH " + this.GetLength();
    }

    public override bool IsGeneric()
    {
        return false;
    }

    public override bool ContainsVoid()
    {
        return false;
    }

    public object? GetIdentifier()
    {
        return null;
    }

    public override string ToCDS()
    {
        return "abap.char( " + this.GetLength() + " )";
    }
}
