namespace ScipSharp.Indexer.Sql.Lexing;

/// <summary>A statement: tokens <c>[Start, End)</c> of a token list, without its terminator.</summary>
public readonly record struct StatementSpan(int Start, int End)
{
    public int Count => End - Start;
}

/// <summary>
/// Splits a token stream into statements. A <c>;</c> ends a statement only outside parentheses and procedural blocks
/// (<c>BEGIN..END</c>, <c>CASE..END</c>, PL/SQL <c>IS|AS..BEGIN..END</c> declaration sections, <c>DECLARE</c>); T-SQL
/// <c>GO</c>, SQL*Plus <c>/</c> and custom MySQL delimiters always end one. In a T-SQL script that uses <c>GO</c>,
/// <c>CREATE PROCEDURE/FUNCTION/TRIGGER</c> extends to the end of its batch.
/// </summary>
public static class StatementSplitter
{
    private enum Frame
    {
        Block,
        Case,
        Declarations,
    }

    public static List<StatementSpan> Split(IReadOnlyList<Token> tokens, SqlDialect dialect, bool procedural = false)
    {
        var result = new List<StatementSpan>();
        var hasGo = dialect == SqlDialect.TSql && tokens.Any(t => t.Kind == TokenKind.BatchSeparator);
        var stack = new Stack<Frame>();
        var parens = 0;
        var start = 0;
        var toBatchEnd = false;
        var expectHeaderIsAs = false;

        void Flush(int end)
        {
            if (end > start)
            {
                result.Add(new StatementSpan(start, end));
            }

            stack.Clear();
            parens = 0;
            toBatchEnd = false;
            expectHeaderIsAs = false;
        }

        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            switch (t.Kind)
            {
                case TokenKind.EndOfFile:
                    Flush(i);
                    return result;
                case TokenKind.BatchSeparator or TokenKind.Delimiter:
                    Flush(i);
                    start = i + 1;
                    continue;
                case TokenKind.Semicolon or TokenKind.Op when t.IsSemi:
                    expectHeaderIsAs = false;
                    if (parens <= 0 && stack.Count == 0 && !toBatchEnd)
                    {
                        Flush(i);
                        start = i + 1;
                    }
                    else if (parens > 0 && stack.Count > 0)
                    {
                        parens = 0; // unbalanced parentheses inside a block: recover
                    }

                    continue;
                case TokenKind.Op when t.Text == "(":
                    parens++;
                    continue;
                case TokenKind.Op when t.Text == ")":
                    parens = Math.Max(0, parens - 1);
                    continue;
                case TokenKind.Word:
                    break;
                default:
                    continue;
            }

            if (i == start)
            {
                toBatchEnd = hasGo && IsBatchScopedCreate(tokens, i);
            }

            var prev = i > start ? tokens[i - 1] : null;
            if (prev is not null && prev.IsOp("."))
            {
                continue; // qualified name part such as t.begin / x.end
            }

            switch (t.Upper)
            {
                case "BEGIN":
                    if (IsBlockBegin(tokens, i, dialect, procedural))
                    {
                        if (stack.Count > 0 && stack.Peek() == Frame.Declarations)
                        {
                            stack.Pop();
                        }

                        stack.Push(Frame.Block);
                    }

                    break;
                case "CASE":
                    stack.Push(Frame.Case);
                    break;
                case "END":
                    {
                        var next = Next(tokens, i);
                        if (next is { IsWord: true } && next.Upper is "IF" or "LOOP" or "WHILE" or "REPEAT" or "FOR")
                        {
                            i++; // END IF / END LOOP close a construct that did not push
                            break;
                        }

                        if (stack.Count > 0)
                        {
                            stack.Pop();
                        }

                        if (next is { IsWord: true } && next.Upper is "CASE" or "TRY" or "CATCH")
                        {
                            i++;
                        }

                        break;
                    }

                case "DECLARE" when dialect is SqlDialect.PlSql or SqlDialect.Postgres:
                    if (stack.Count == 0 || stack.Peek() != Frame.Declarations)
                    {
                        stack.Push(Frame.Declarations);
                    }

                    break;
                case "PROCEDURE" or "FUNCTION" or "PACKAGE" or "TRIGGER" when dialect == SqlDialect.PlSql:
                    expectHeaderIsAs = true;
                    break;
                case "BODY" when dialect == SqlDialect.PlSql && prev is { Upper: "TYPE" }:
                    expectHeaderIsAs = true;
                    break;
                case "IS" or "AS" when expectHeaderIsAs && parens == 0:
                    expectHeaderIsAs = false;
                    if (Next(tokens, i) is { } n && (n.Is("LANGUAGE") || n.Is("EXTERNAL")))
                    {
                        break; // call spec: no body
                    }

                    stack.Push(Frame.Declarations);
                    break;
                case "COMPOUND" when dialect == SqlDialect.PlSql && Next(tokens, i) is { Upper: "TRIGGER" }:
                    stack.Push(Frame.Declarations);
                    i++;
                    break;
            }
        }

        Flush(tokens.Count);
        return result;
    }

    private static Token? Next(IReadOnlyList<Token> tokens, int i) => i + 1 < tokens.Count ? tokens[i + 1] : null;

    /// <summary>True for a <c>BEGIN</c> that opens a block rather than a transaction.</summary>
    private static bool IsBlockBegin(IReadOnlyList<Token> tokens, int i, SqlDialect dialect, bool procedural)
    {
        var next = Next(tokens, i);
        if (next is null || next.IsTerminator)
        {
            return false;
        }

        if (next.IsWord && next.Upper is "TRANSACTION" or "TRAN" or "WORK" or "ISOLATION" or "READ" or "DEFERRED" or "IMMEDIATE" or "EXCLUSIVE" or "DISTRIBUTED" or "NOT" or "DEFERRABLE" or "DIALOG" or "CONVERSATION")
        {
            return false;
        }

        // PostgreSQL: a top-level BEGIN is a transaction unless it is BEGIN ATOMIC (function bodies are lexed separately).
        if (dialect == SqlDialect.Postgres && !procedural && (i == 0 || tokens[i - 1].IsTerminator) && !next.Is("ATOMIC"))
        {
            return false;
        }

        return true;
    }

    private static bool IsBatchScopedCreate(IReadOnlyList<Token> tokens, int i)
    {
        if (!tokens[i].Is("CREATE") && !tokens[i].Is("ALTER"))
        {
            return false;
        }

        var j = i + 1;
        if (j + 1 < tokens.Count && tokens[j].Is("OR"))
        {
            j += 2;
        }

        return j < tokens.Count && tokens[j].Upper is "PROC" or "PROCEDURE" or "FUNCTION" or "TRIGGER";
    }
}
