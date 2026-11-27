// Ported from abaplint packages/core/src/abap/1_lexer/lexer_result.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Lexer;

public record IABAPLexerResult(IFile File, IReadOnlyList<AbstractToken> Tokens);
