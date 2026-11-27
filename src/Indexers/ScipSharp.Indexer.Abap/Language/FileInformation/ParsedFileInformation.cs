// Ported from abaplint packages/core/src/abap/4_file_information/parsed_file_information.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
namespace ScipSharp.Indexer.Abap.FileInformation;

public sealed class ParsedFileInformation
{
    public List<InfoInterfaceDefinition> Interfaces { get; set; } = [];
    public List<InfoClassDefinition> Classes { get; set; } = [];
    public List<InfoFormDefinition> Forms { get; set; } = [];
    public List<InfoClassImplementation> Implementations { get; set; } = [];
}
