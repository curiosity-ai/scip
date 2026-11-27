// C# addition, not a port of an upstream file: stands in for the upstream object classes that are not
// ported (the many non ABAP, non DDIC types of packages/core/src/objects), so the registry still knows
// the object and its files. No parsing, no description, no parsing issues.
namespace ScipSharp.Indexer.Abap.Objects;

public class GenericObject : AbstractObject
{
    private readonly string type;

    public GenericObject(string name, string type)
        : base(name)
    {
        this.type = type;
    }

    public override string GetType()
    {
        return this.type;
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 100, AllowNamespace = true };
    }
}
