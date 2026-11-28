using System.Text;

namespace ScipSharp.Indexer.Cobol.Syntax;

/// <summary>A <c>COPY</c> (or <c>EXEC SQL INCLUDE</c> / <c>++INCLUDE</c>) statement found while preprocessing.</summary>
/// <param name="NameToken">The token naming the copybook.</param>
/// <param name="TargetFileId">The resolved copybook, or -1.</param>
internal sealed record CopyDirective(Token NameToken, int TargetFileId);

/// <summary>Result of preprocessing one compilation unit.</summary>
internal sealed record PreprocessedSource(List<Token> Tokens, List<CopyDirective> Copies, HashSet<int> IncludedFiles);

/// <summary>How a REPLACING operand matches.</summary>
internal enum ReplaceMode
{
    Full,
    Partial,
    Leading,
    Trailing,
}

/// <summary>One <c>==a== BY ==b==</c> pair.</summary>
internal sealed record ReplacingPair(IReadOnlyList<Token> Left, IReadOnlyList<Token> Right, ReplaceMode Mode)
{
    public string LeftText { get; } = string.Concat(Left.Select(t => t.Text));

    public string RightText { get; } = string.Join(' ', Right.Select(t => t.Text));
}

/// <summary>
/// The COBOL text-manipulation phase: expands <c>COPY name [OF|IN lib] [SUPPRESS] [REPLACING ...]</c> recursively (with
/// cycle and depth protection), <c>EXEC SQL INCLUDE name END-EXEC</c> and <c>++INCLUDE name</c>, applies REPLACING
/// (whole text-word sequences, IBM-style partial words delimited by <c>:TAG:</c> or <c>(TAG)</c>, and COBOL 2002
/// LEADING/TRAILING) and then <c>REPLACE ... . / REPLACE OFF.</c> to the expanded text. Drops <c>EJECT</c>,
/// <c>SKIPn</c> and <c>TITLE</c>.
/// </summary>
internal sealed class Preprocessor(
    Func<int, IReadOnlyList<Token>> lexFile, Func<string, string?, int, int> resolve, Action<string>? warn = null, int maxExpandedTokens = Preprocessor.MaxExpandedTokens)
{
    private const int MaxDepth = 32;

    /// <summary>
    /// Upper bound on the copybook tokens expanded into one compilation unit. The cycle guard only looks at the current
    /// COPY path, so a diamond-shaped graph (each copybook copying the next one twice) legitimately expands to 2^depth
    /// copies; past this budget further COPY statements are left unexpanded (with a warning) instead of hanging.
    /// </summary>
    internal const int MaxExpandedTokens = 1_000_000;

    private int _expandedTokens;

    /// <summary>Copybook names supplied by the DB2 precompiler; not finding them is normal.</summary>
    private static readonly HashSet<string> SystemIncludes = new(StringComparer.OrdinalIgnoreCase) { "SQLCA", "SQLDA", "SQLIMSCA" };

    public PreprocessedSource Process(int fileId, IReadOnlyList<Token> tokens)
    {
        var copies = new List<CopyDirective>();
        var included = new HashSet<int>();
        _expandedTokens = 0;
        var expanded = Expand(tokens, fileId, -1, 0, [fileId], copies, included);
        return new PreprocessedSource(ApplyReplaceStatements(expanded), copies, included);
    }

    private List<Token> Expand(IReadOnlyList<Token> tokens, int fileId, int origin, int depth, List<int> stack, List<CopyDirective> copies, HashSet<int> included)
    {
        var output = new List<Token>(tokens.Count);
        var i = 0;
        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Is("COPY") && i + 1 < tokens.Count && tokens[i + 1].Kind is TokenKind.Word or TokenKind.Literal)
            {
                i = CopyStatement(tokens, i, fileId, origin, depth, stack, copies, included, output);
                continue;
            }

            if (t.Is("++INCLUDE") && i + 1 < tokens.Count && tokens[i + 1].Kind is TokenKind.Word or TokenKind.Literal)
            {
                Include(tokens[i + 1], null, [], fileId, origin, depth, stack, copies, included, output);
                i += 2;
                if (i < tokens.Count && tokens[i].Kind == TokenKind.Period)
                {
                    i++;
                }

                continue;
            }

            if (t.Is("EXEC") && i + 4 < tokens.Count && tokens[i + 1].Is("SQL") && tokens[i + 2].Is("INCLUDE")
                && tokens[i + 3].Kind is TokenKind.Word or TokenKind.Literal && tokens[i + 4].Is("END-EXEC"))
            {
                Include(tokens[i + 3], null, [], fileId, origin, depth, stack, copies, included, output);
                i += 5;
                continue;
            }

            if ((t.Is("EJECT") || t.Is("SKIP1") || t.Is("SKIP2") || t.Is("SKIP3")) && (i == 0 || tokens[i - 1].Kind == TokenKind.Period || t.FirstOnLine))
            {
                i++;
                if (i < tokens.Count && tokens[i].Kind == TokenKind.Period)
                {
                    i++;
                }

                continue;
            }

            if (t.Is("TITLE") && i + 1 < tokens.Count && tokens[i + 1].Kind == TokenKind.Literal)
            {
                i += 2;
                if (i < tokens.Count && tokens[i].Kind == TokenKind.Period)
                {
                    i++;
                }

                continue;
            }

            output.Add(t);
            i++;
        }

        return output;
    }

    private int CopyStatement(IReadOnlyList<Token> tokens, int i, int fileId, int origin, int depth, List<int> stack,
        List<CopyDirective> copies, HashSet<int> included, List<Token> output)
    {
        var name = tokens[i + 1];
        var j = i + 2;
        string? library = null;
        if (j + 1 < tokens.Count && (tokens[j].Is("OF") || tokens[j].Is("IN")) && tokens[j + 1].Kind is TokenKind.Word or TokenKind.Literal)
        {
            library = tokens[j + 1].Value;
            j += 2;
        }

        while (j < tokens.Count && (tokens[j].Is("SUPPRESS") || tokens[j].Is("PRINTING")))
        {
            j++;
        }

        var pairs = new List<ReplacingPair>();
        if (j < tokens.Count && tokens[j].Is("REPLACING"))
        {
            j = ParsePairs(tokens, j + 1, pairs);
        }

        if (j < tokens.Count && tokens[j].Kind == TokenKind.Period)
        {
            j++;
        }

        Include(name, library, pairs, fileId, origin, depth, stack, copies, included, output);
        return j;
    }

    /// <summary>Parses <c>operand BY operand ...</c> up to (not including) the terminating period.</summary>
    internal static int ParsePairs(IReadOnlyList<Token> tokens, int j, List<ReplacingPair> pairs)
    {
        while (j < tokens.Count && tokens[j].Kind != TokenKind.Period)
        {
            var mode = ReplaceMode.Full;
            if (tokens[j].Is("LEADING"))
            {
                mode = ReplaceMode.Leading;
                j++;
            }
            else if (tokens[j].Is("TRAILING"))
            {
                mode = ReplaceMode.Trailing;
                j++;
            }

            if (j >= tokens.Count)
            {
                break;
            }

            var (left, afterLeft, leftIsPseudo) = Operand(tokens, j);
            if (left is null || afterLeft >= tokens.Count || !tokens[afterLeft].Is("BY"))
            {
                // Malformed: skip to the next period.
                while (j < tokens.Count && tokens[j].Kind != TokenKind.Period)
                {
                    j++;
                }

                break;
            }

            var (right, afterRight, _) = Operand(tokens, afterLeft + 1);
            if (right is null)
            {
                break;
            }

            if (mode == ReplaceMode.Full && leftIsPseudo && left.Count > 0)
            {
                var text = string.Concat(left.Select(t => t.Text));
                if ((text[0] is ':' or '(' || text[^1] is ':' or ')') && left.Skip(1).All(t => !t.SpaceBefore))
                {
                    mode = ReplaceMode.Partial;
                }
            }

            if (left.Count > 0)
            {
                pairs.Add(new ReplacingPair(left, right, mode));
            }

            j = afterRight;
            while (j < tokens.Count && tokens[j].Kind == TokenKind.Comma)
            {
                j++;
            }
        }

        return j;
    }

    private static (List<Token>? Tokens, int Next, bool Pseudo) Operand(IReadOnlyList<Token> tokens, int j)
    {
        if (j >= tokens.Count)
        {
            return (null, j, false);
        }

        if (tokens[j].Kind == TokenKind.PseudoDelimiter)
        {
            var list = new List<Token>();
            var k = j + 1;
            while (k < tokens.Count && tokens[k].Kind != TokenKind.PseudoDelimiter)
            {
                if (tokens[k].Kind != TokenKind.Comma)
                {
                    list.Add(tokens[k]);
                }

                k++;
            }

            return (list, Math.Min(k + 1, tokens.Count), true);
        }

        if (tokens[j].Kind == TokenKind.Period)
        {
            return (null, j, false);
        }

        // identifier [OF|IN qualifier]... | literal | word
        var operand = new List<Token> { tokens[j] };
        var n = j + 1;
        while (n + 1 < tokens.Count && (tokens[n].Is("OF") || tokens[n].Is("IN")) && tokens[n + 1].IsWord)
        {
            operand.Add(tokens[n]);
            operand.Add(tokens[n + 1]);
            n += 2;
        }

        return (operand, n, false);
    }

    private void Include(Token name, string? library, List<ReplacingPair> pairs, int fileId, int origin, int depth, List<int> stack,
        List<CopyDirective> copies, HashSet<int> included, List<Token> output)
    {
        var target = resolve(name.Value, library, fileId);
        copies.Add(new CopyDirective(name, target));
        if (target < 0)
        {
            if (!SystemIncludes.Contains(name.Value))
            {
                warn?.Invoke($"copybook not found: {name.Value}");
            }

            return;
        }

        if (stack.Contains(target) || depth >= MaxDepth)
        {
            warn?.Invoke($"recursive COPY of {name.Value} ignored");
            return;
        }

        var source = lexFile(target);
        if (_expandedTokens + source.Count > maxExpandedTokens)
        {
            warn?.Invoke($"COPY of {name.Value} ignored: more than {maxExpandedTokens} copybook tokens expanded");
            return;
        }

        _expandedTokens += source.Count;
        included.Add(target);
        var copyTokens = source.Select(t => t with { Origin = target }).ToList();
        if (pairs.Count > 0)
        {
            copyTokens = ApplyReplacing(copyTokens, pairs, target);
        }

        stack.Add(target);
        output.AddRange(Expand(copyTokens, target, target, depth + 1, stack, copies, included));
        stack.RemoveAt(stack.Count - 1);
    }

    /// <summary>Applies REPLACE statements (after COPY expansion).</summary>
    private static List<Token> ApplyReplaceStatements(List<Token> tokens)
    {
        if (!tokens.Any(t => t.Is("REPLACE")))
        {
            return tokens;
        }

        var output = new List<Token>(tokens.Count);
        var segment = new List<Token>();
        List<ReplacingPair> active = [];
        var i = 0;
        while (i < tokens.Count)
        {
            var t = tokens[i];
            if (t.Is("REPLACE") && i + 1 < tokens.Count && (tokens[i + 1].Kind == TokenKind.PseudoDelimiter
                || tokens[i + 1].Is("OFF") || tokens[i + 1].Is("ALSO") || tokens[i + 1].Is("LAST") || tokens[i + 1].Is("LEADING") || tokens[i + 1].Is("TRAILING")))
            {
                output.AddRange(active.Count > 0 ? ApplyReplacing(segment, active, -1) : segment);
                segment = [];
                var j = i + 1;
                if (tokens[j].Is("LAST"))
                {
                    j++;
                }

                if (tokens[j].Is("OFF"))
                {
                    active = [];
                    j++;
                }
                else
                {
                    if (tokens[j].Is("ALSO"))
                    {
                        j++;
                    }

                    var pairs = new List<ReplacingPair>();
                    j = ParsePairs(tokens, j, pairs);
                    active = pairs;
                }

                if (j < tokens.Count && tokens[j].Kind == TokenKind.Period)
                {
                    j++;
                }

                i = j;
                continue;
            }

            segment.Add(t);
            i++;
        }

        output.AddRange(active.Count > 0 ? ApplyReplacing(segment, active, -1) : segment);
        return output;
    }

    /// <summary>Applies REPLACING pairs to a token list. <paramref name="origin"/> is set on synthesized tokens (-1 keeps the replaced token's).</summary>
    internal static List<Token> ApplyReplacing(List<Token> tokens, IReadOnlyList<ReplacingPair> pairs, int origin)
    {
        var partial = pairs.Where(p => p.Mode != ReplaceMode.Full).ToList();
        var full = pairs.Where(p => p.Mode == ReplaceMode.Full).ToList();
        if (partial.Count > 0)
        {
            tokens = ApplyPartial(tokens, partial);
        }

        if (full.Count == 0)
        {
            return tokens;
        }

        var output = new List<Token>(tokens.Count);
        var i = 0;
        while (i < tokens.Count)
        {
            var matched = false;
            foreach (var pair in full)
            {
                var n = Match(tokens, i, pair.Left);
                if (n == 0)
                {
                    continue;
                }

                var replaced = tokens.Skip(i).Take(n).Where(t => t.Kind != TokenKind.Comma).ToList();
                for (var r = 0; r < pair.Right.Count; r++)
                {
                    var source = pair.Right.Count == replaced.Count ? replaced[r] : r == 0 ? replaced[0] : null;
                    output.Add(pair.Right[r] with
                    {
                        Location = source?.Location,
                        Origin = origin >= 0 ? origin : replaced[0].Origin,
                        SpaceBefore = r == 0 ? replaced[0].SpaceBefore : pair.Right[r].SpaceBefore,
                        FirstOnLine = r == 0 && replaced[0].FirstOnLine,
                        AreaA = r == 0 && replaced[0].AreaA,
                    });
                }

                i += n;
                matched = true;
                break;
            }

            if (!matched)
            {
                output.Add(tokens[i]);
                i++;
            }
        }

        return output;
    }

    /// <summary>Returns the number of source tokens matched by <paramref name="pattern"/> at <paramref name="at"/> (commas are skipped), or 0.</summary>
    private static int Match(List<Token> tokens, int at, IReadOnlyList<Token> pattern)
    {
        var k = at;
        foreach (var p in pattern)
        {
            while (k < tokens.Count && tokens[k].Kind == TokenKind.Comma && k > at)
            {
                k++;
            }

            if (k >= tokens.Count || !SameText(tokens[k], p))
            {
                return 0;
            }

            k++;
        }

        return k - at;
    }

    private static bool SameText(Token a, Token b) =>
        a.Kind == TokenKind.Literal || b.Kind == TokenKind.Literal ? a.Text == b.Text : a.Upper == b.Upper;

    /// <summary>
    /// Partial-word replacement on "clusters" (runs of tokens with no whitespace between them, e.g. <c>:TAG:-NAME</c> or
    /// <c>FLG-(VAR)-OK</c>). A changed cluster is re-lexed; a single resulting token keeps the cluster's location.
    /// </summary>
    private static List<Token> ApplyPartial(List<Token> tokens, List<ReplacingPair> pairs)
    {
        var output = new List<Token>(tokens.Count);
        var i = 0;
        while (i < tokens.Count)
        {
            if (!InCluster(tokens[i]))
            {
                output.Add(tokens[i]);
                i++;
                continue;
            }

            var end = i + 1;
            while (end < tokens.Count && InCluster(tokens[end]) && !tokens[end].SpaceBefore && SameLine(tokens[end - 1], tokens[end]))
            {
                end++;
            }

            var sb = new StringBuilder();
            for (var k = i; k < end; k++)
            {
                sb.Append(tokens[k].Text);
            }

            var original = sb.ToString();
            var text = original;
            foreach (var pair in pairs)
            {
                text = pair.Mode switch
                {
                    ReplaceMode.Leading => text.StartsWith(pair.LeftText, StringComparison.OrdinalIgnoreCase) ? pair.RightText + text[pair.LeftText.Length..] : text,
                    ReplaceMode.Trailing => text.EndsWith(pair.LeftText, StringComparison.OrdinalIgnoreCase) ? text[..^pair.LeftText.Length] + pair.RightText : text,
                    _ => text.Replace(pair.LeftText, pair.RightText, StringComparison.OrdinalIgnoreCase),
                };
            }

            if (text == original)
            {
                for (var k = i; k < end; k++)
                {
                    output.Add(tokens[k]);
                }

                i = end;
                continue;
            }

            var first = tokens[i];
            var last = tokens[end - 1];
            var sameLine = first.Location is { } a && last.Location is { } b && a.Line == b.Line && b.End - a.Start == original.Length;

            // Map positions of the re-lexed text back to the cluster: unchanged prefix and suffix keep their columns,
            // the first token overlapping the changed middle gets the changed span.
            var prefix = 0;
            while (prefix < original.Length && prefix < text.Length && original[prefix] == text[prefix])
            {
                prefix++;
            }

            var suffix = 0;
            while (suffix < original.Length - prefix && suffix < text.Length - prefix && original[^(suffix + 1)] == text[^(suffix + 1)])
            {
                suffix++;
            }

            var relexed = Lexer.LexFragment(text);
            var changedAssigned = false;
            for (var r = 0; r < relexed.Count; r++)
            {
                SourceLocation? location = null;
                if (sameLine && relexed[r].Location is { } rel && rel.Line == 0)
                {
                    var origin = first.Location!.Value;
                    var delta = original.Length - text.Length;
                    int? start = null, stop = null;
                    if (rel.End <= prefix)
                    {
                        (start, stop) = (rel.Start, rel.End);
                    }
                    else if (rel.Start >= text.Length - suffix)
                    {
                        (start, stop) = (rel.Start + delta, rel.End + delta);
                    }
                    else if (!changedAssigned)
                    {
                        changedAssigned = true;
                        start = rel.Start < prefix ? rel.Start : prefix;
                        stop = rel.End > text.Length - suffix ? rel.End + delta : original.Length - suffix;
                    }

                    if (start is { } s0 && stop is { } s1 && s1 > s0)
                    {
                        location = new SourceLocation(origin.FileId, origin.Line, origin.Start + s0, origin.Start + s1);
                    }
                }
                else if (r == 0)
                {
                    location = first.Location;
                }

                output.Add(relexed[r] with
                {
                    Location = location,
                    Origin = first.Origin,
                    SpaceBefore = r == 0 ? first.SpaceBefore : relexed[r].SpaceBefore,
                    FirstOnLine = r == 0 && first.FirstOnLine,
                    AreaA = r == 0 && first.AreaA,
                });
            }

            i = end;
        }

        return output;
    }

    private static bool InCluster(Token t) => t.Kind is not (TokenKind.Period or TokenKind.Comma or TokenKind.PseudoDelimiter);

    private static bool SameLine(Token a, Token b) =>
        a.Location is { } x && b.Location is { } y ? x.Line == y.Line && x.FileId == y.FileId : a.Location is null && b.Location is null;
}
