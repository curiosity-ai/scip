// Ported from abaplint packages/core/src/abap/types/basic/data_reference_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public class DataReference : AbstractType
{
    private readonly AbstractType type;

    public DataReference(AbstractType type, string? qualifiedName = null)
        : base(new AbstractTypeData { QualifiedName = qualifiedName })
    {
        this.type = type;
    }

    public override string ToText(int level)
    {
        return "Data REF TO " + this.type.ToText(level + 1);
    }

    public new AbstractType GetType()
    {
        return this.type;
    }

    public override string ToABAP()
    {
        var type = this.type.ToABAP();
        if (type.Contains(" TABLE OF ", StringComparison.Ordinal))
        {
            return "";
            // hmm, should this return undefined?
        }
        return "REF TO " + this.type.ToABAP();
    }

    public override bool IsGeneric()
    {
        if (this.type is AnyType)
        {
            return true;
        }
        // LIKE REF TO a generic table, eg. a field symbol TYPE STANDARD TABLE
        if (this.type is TableType && ((TableType)this.type).IsGeneric())
        {
            return true;
        }
        return false;
    }

    public override bool ContainsVoid()
    {
        return this.type.ContainsVoid();
    }

    public override string ToCDS()
    {
        return "abap.TODO_REFERENCE";
    }
}
