// Ported from abaplint packages/core/src/abap/types/basic/table_type.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Types.Basic;

public static class TableAccessType
{
    public const string standard = "STANDARD";
    public const string sorted = "SORTED";
    public const string hashed = "HASHED";
    public const string index = "INDEX";
    public const string any = "ANY";
}

public static class TableKeyType
{
    public const string @default = "DEFAULT";
    public const string user = "USER";
    public const string empty = "EMPTY";
}

public class ITableKey
{
    public string Name { get; set; }
    public string Type { get; set; }
    public List<string> KeyFields { get; set; }
    public bool IsUnique { get; set; }
}

public class ITableOptions
{
    public bool WithHeader { get; set; }
    public string KeyType { get; set; }
    public ITableKey? PrimaryKey { get; set; }
    public List<ITableKey>? Secondary { get; set; }
}

public class TableType : AbstractType
{
    private readonly AbstractType rowType;
    private readonly ITableOptions options;

    public TableType(AbstractType rowType, ITableOptions options, string? qualifiedName = null, string? description = null)
        : base(new AbstractTypeData { QualifiedName = qualifiedName, Description = description })
    {
        this.rowType = rowType;
        this.options = options;
        if (options.PrimaryKey?.Type == TableAccessType.standard && options.PrimaryKey.IsUnique == true)
        {
            throw new Exception("STANDARD tables cannot have UNIQUE key");
        }
    }

    public ITableOptions GetOptions()
    {
        return this.options;
    }

    public bool IsWithHeader()
    {
        return this.options.WithHeader;
    }

    public string? GetAccessType()
    {
        return this.options.PrimaryKey?.Type;
    }

    public AbstractType GetRowType()
    {
        return this.rowType;
    }

    public override string ToABAP()
    {
        // todo, this is used for downport, so use default key for now
        return "STANDARD TABLE OF " + this.rowType.ToABAP() + " WITH DEFAULT KEY";
    }

    public override string ToText(int level)
    {
        var type = this.rowType;

        if (this.options.WithHeader == true)
        {
            return "Table with header of " + type.ToText(level + 1);
        }
        else
        {
            return "Table of " + type.ToText(level + 1);
        }
    }

    public override bool IsGeneric()
    {
        if (this.options.PrimaryKey?.Type != TableAccessType.standard && this.options.KeyType == TableKeyType.user && this.options.PrimaryKey?.KeyFields.Count == 0)
        {
            return true;
        }
        return this.rowType.IsGeneric();
    }

    public override bool ContainsVoid()
    {
        return this.rowType.ContainsVoid();
    }

    public override string ToCDS()
    {
        return "abap.TODO_TABLE";
    }
}
