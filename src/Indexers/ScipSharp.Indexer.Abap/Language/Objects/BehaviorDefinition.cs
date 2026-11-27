// Ported from abaplint packages/core/src/objects/behavior_definition.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using System.Text.RegularExpressions;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Objects;

public class ParsedBehaviorDefinition
{
    /// <summary>entities defined in the behavior definition, alias is null if not specified</summary>
    public List<(string name, string? alias)> Entities { get; set; } = new();
}

public class BehaviorDefinition : AbstractObject
{
    private ParsedBehaviorDefinition? parsedData = null;

    public BehaviorDefinition(string name)
        : base(name)
    {
    }

    public override string GetType()
    {
        return "BDEF";
    }

    public override IAllowedNaming GetAllowedNaming()
    {
        // todo, verify
        return new IAllowedNaming { MaxLength = 30, AllowNamespace = true };
    }

    public override void SetDirty()
    {
        this.parsedData = null;
        base.SetDirty();
    }

    public IFile? FindSourceFile()
    {
        return this.GetFiles().FirstOrDefault(f => f.GetFilename().EndsWith(".asbdef", StringComparison.Ordinal));
    }

    public List<(string name, string? alias)> ListEntities()
    {
        return this.ParseSource().Entities;
    }

    /// <summary>finds the entity name for a given alias, null if the alias is not defined</summary>
    public string? FindEntityNameByAlias(string alias)
    {
        var upper = JsString.ToUpper(alias);
        foreach (var entity in this.ListEntities())
        {
            if (JsUtils.ToUpper(entity.alias) == upper)
            {
                return entity.name;
            }
        }
        return null;
    }

    /////////////////////////

    private ParsedBehaviorDefinition ParseSource()
    {
        if (this.parsedData != null)
        {
            return this.parsedData;
        }

        this.parsedData = new ParsedBehaviorDefinition();

        var raw = this.FindSourceFile()?.GetRaw();
        if (raw == null)
        {
            return this.parsedData;
        }

        // BDEF uses "//" for line comments
        var stripped = JsUtils.RegexReplace(raw, @"\/\/.*$", "", RegexOptions.ECMAScript | RegexOptions.Multiline);
        // eg. "define behavior for ZI_Booking alias Booking", also "define abstract behavior for ..."
        var regex = new Regex(@"\bdefine\s+(?:\w+\s+)*?behavior\s+for\s+([\w/]+)(?:\s+alias\s+(\w+))?", RegexOptions.ECMAScript | RegexOptions.IgnoreCase);
        foreach (Match match in regex.Matches(stripped))
        {
            this.parsedData.Entities.Add((match.Groups[1].Value, match.Groups[2].Success ? match.Groups[2].Value : null));
        }

        return this.parsedData;
    }
}
