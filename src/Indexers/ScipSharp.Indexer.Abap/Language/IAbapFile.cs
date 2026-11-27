// Ported from abaplint packages/core/src/abap/iabap_file.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.FileInformation;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;

namespace ScipSharp.Indexer.Abap;

public interface IABAPFile
{
    IABAPFileInformation GetInfo();
    StructureNode? GetStructure();
    IReadOnlyList<AbstractToken> GetTokens(bool withPragmas = true);
    IReadOnlyList<StatementNode> GetStatements();
}
