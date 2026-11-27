// Ported from abaplint packages/core/src/objects/_unknown_object.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.Objects;

public class UnknownObject : AbstractObject
{
    private readonly string type;

    public UnknownObject(string name, string type)
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

    public override List<Issue> GetParsingIssues()
    {
        var pos = new Position(1, 1);
        var file = this.GetFiles()[0]!;
        var message = "Unknown object type, currently not supported in abaplint, open issue on github";
        var issue = Issue.AtPosition(file, pos, message, "parser_error", Severity.Error);
        return new List<Issue> { issue };
    }
}
