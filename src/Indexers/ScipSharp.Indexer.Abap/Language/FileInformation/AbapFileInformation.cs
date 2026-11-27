// Ported from abaplint packages/core/src/abap/4_file_information/abap_file_information.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.FileInformation;

public class ABAPFileInformation : IABAPFileInformation
{
    private readonly ParsedFileInformation parsed;

    public ABAPFileInformation(ParsedFileInformation parsed)
    {
        this.parsed = parsed;
    }

    public IReadOnlyList<InfoClassImplementation> ListClassImplementations() => parsed.Implementations;

    public IReadOnlyList<InfoInterfaceDefinition> ListInterfaceDefinitions() => parsed.Interfaces;

    public InfoInterfaceDefinition? GetInterfaceDefinitionByName(string name)
    {
        var upper = JsString.ToUpper(name);
        foreach (var i in ListInterfaceDefinitions())
        {
            if (JsString.ToUpper(i.Identifier.GetName()) == upper)
            {
                return i;
            }
        }
        return null;
    }

    public IReadOnlyList<InfoClassDefinition> ListClassDefinitions() => parsed.Classes;

    public InfoClassDefinition? GetClassDefinitionByName(string name)
    {
        var upper = JsString.ToUpper(name);
        foreach (var d in ListClassDefinitions())
        {
            if (JsString.ToUpper(d.Identifier.GetName()) == upper)
            {
                return d;
            }
        }
        return null;
    }

    public InfoClassImplementation? GetClassImplementationByName(string name)
    {
        var upper = JsString.ToUpper(name);
        foreach (var impl in ListClassImplementations())
        {
            if (JsString.ToUpper(impl.Identifier.GetName()) == upper)
            {
                return impl;
            }
        }
        return null;
    }

    public IReadOnlyList<InfoFormDefinition> ListFormDefinitions() => parsed.Forms;
}
