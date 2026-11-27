// Ported from abaplint packages/core/src/abap/2_statements/statement_result.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Statements;

public sealed record IStatementResult(IFile File, IReadOnlyList<AbstractToken> Tokens, IReadOnlyList<StatementNode> Statements)
    : IABAPLexerResult(File, Tokens);
