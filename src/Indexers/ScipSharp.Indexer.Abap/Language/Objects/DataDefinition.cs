// Ported from abaplint packages/core/src/objects/data_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
// TODO: the CDS parser (packages/core/src/cds) is not ported. A DDLS therefore always behaves as one
// whose source does not parse upstream: no fields, parseType() is VoidType("DDLS:todo"), hasParserError() is true.
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Types.Basic;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Objects;

public class ParsedDataDefinitionField
{
    public bool Key { get; set; }
    public string Name { get; set; }
    /// <summary>the name in the prefixed source, if aliased via "as"</summary>
    public string? NameInSource { get; set; }
    public string Prefix { get; set; }
    public List<string> Annotations { get; set; } = new();
}

public class ParsedDataDefinition
{
    public string? SqlViewName { get; set; }
    public string? DefinitionName { get; set; }
    public string? Description { get; set; }
    public List<ParsedDataDefinitionField> Fields { get; set; } = new();
    public List<(string name, string? @as)> Sources { get; set; } = new();
    public List<(string name, string? @as)> Associations { get; set; } = new();
    public List<(string name, string? @as)> Relations { get; set; } = new();
    public ExpressionNode? Tree { get; set; }
}

public class DataDefinition : AbstractObject, IParseTypeObject
{
    private bool? parserError = null;
    private ParsedDataDefinition? parsedData = null;
    private AbstractType? typeCache = null;
    private ParsedDataDefinition? typeCacheData = null;

    public DataDefinition(string name) : base(name)
    {
    }

    public override string GetType()
    {
        return "DDLS";
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        return new IAllowedNaming { MaxLength = 40, AllowNamespace = true };
    }

    public string? GetSQLViewName()
    {
        this.Parse();
        return this.parsedData?.SqlViewName;
    }

    public string? GetDefinitionName()
    {
        this.Parse();
        return this.parsedData?.DefinitionName;
    }

    public override string? GetDescription()
    {
        this.Parse();
        return this.parsedData?.Description;
    }

    public AbstractType ParseType(IRegistry reg)
    {
        this.Parse();
        if (this.typeCache == null || this.typeCacheData != this.parsedData)
        {
            this.typeCacheData = this.parsedData;
            // CDSDetermineTypes.parseType: no fields gives VoidType("DDLS:todo")
            this.typeCache = VoidType.Get("DDLS:todo");
        }
        return this.typeCache;
    }

    public ParsedDataDefinition? GetParsedData()
    {
        return this.parsedData;
    }

    public List<(string name, string? @as)>? ListSources()
    {
        this.Parse();
        return this.parsedData?.Sources;
    }

    public override void SetDirty()
    {
        this.parsedData = null;
        this.typeCache = null;
        this.typeCacheData = null;
        this.parserError = null;
        base.SetDirty();
    }

    public IFile? FindSourceFile()
    {
        return this.GetFiles().FirstOrDefault(f => f.GetFilename().EndsWith(".asddls", StringComparison.Ordinal) || f.GetFilename().EndsWith(".acds", StringComparison.Ordinal));
    }

    public bool? HasParserError()
    {
        return this.parserError;
    }

    public List<string> ListKeys()
    {
        this.Parse();

        var ret = new List<string>();
        foreach (var field in this.parsedData?.Fields ?? new List<ParsedDataDefinitionField>())
        {
            if (field.Key == true)
            {
                ret.Add(field.Name);
            }
        }
        return ret;
    }

    public override IParseResult Parse(ABAPRelease? _release = null, IReadOnlyList<string>? _globalMacros = null, IRegistry? _reg = null, LanguageVersion? _languageVersion = null)
    {
        if (this.IsDirty() == false)
        {
            return new IParseResult { Updated = false, Runtime = 0 };
        }

        this.parsedData = new ParsedDataDefinition
        {
            SqlViewName = null,
            DefinitionName = null,
            Description = this.FindDescription(),
        };

        this.FindSQLViewName();

        // TODO: CDSParser not ported, the tree is always undefined
        this.parsedData.Tree = null;
        this.parserError = true;

        this.dirty = false;
        return new IParseResult { Updated = true, Runtime = 0 };
    }

    public ExpressionNode? GetTree()
    {
        return this.parsedData?.Tree;
    }

    private void FindSQLViewName()
    {
        var raw = this.FindSourceFile()?.GetRaw();
        var match = raw == null ? null : System.Text.RegularExpressions.Regex.Match(raw, @"@AbapCatalog\.sqlViewName: '([\w/]+)'", System.Text.RegularExpressions.RegexOptions.ECMAScript);
        if (match != null && match.Success)
        {
            this.parsedData!.SqlViewName = JsUtils.ToUpper(match.Groups[1].Value);
        }
    }

    private string? FindDescription()
    {
        var raw = this.FindSourceFile()?.GetRaw();
        var match = raw == null ? null : System.Text.RegularExpressions.Regex.Match(raw, @"@EndUserText\.label: '([\w,.:-=#%&() ]+)'", System.Text.RegularExpressions.RegexOptions.ECMAScript);
        if (match != null && match.Success)
        {
            return match.Groups[1].Value;
        }
        return null;
    }
}
