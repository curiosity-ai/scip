// Ported from abaplint packages/core/src/abap/abap_file.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap;

public class ABAPFile : AbstractFile, IABAPFile
{
    private readonly IReadOnlyList<AbstractToken> tokens;
    private readonly IReadOnlyList<StatementNode> statements;
    private readonly StructureNode? structure;
    private readonly IFile file;
    private readonly IABAPFileInformation info;

    public ABAPFile(IFile file,
                    IReadOnlyList<AbstractToken> tokens,
                    IReadOnlyList<StatementNode> statements,
                    StructureNode? structure,
                    IABAPFileInformation info) : base(file.GetFilename())
    {
        this.file = file;
        this.tokens = tokens;
        this.statements = statements;
        this.structure = structure;
        this.info = info;
    }

    public override string GetRaw() => file.GetRaw();

    public override string[] GetRawRows() => file.GetRawRows();

    public IABAPFileInformation GetInfo() => info;

    public StructureNode? GetStructure() => structure;

    public IReadOnlyList<AbstractToken> GetTokens(bool withPragmas = true)
    {
        if (withPragmas)
        {
            return tokens;
        }
        return tokens.Where(t => t is not Pragma).ToList();
    }

    public IReadOnlyList<StatementNode> GetStatements() => statements;
}
