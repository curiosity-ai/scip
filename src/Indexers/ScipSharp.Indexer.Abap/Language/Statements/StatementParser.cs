// Ported from abaplint packages/core/src/abap/2_statements/statement_parser.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer;
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Statements;

using E = ScipSharp.Indexer.Abap.Statements.Expressions;
using S = ScipSharp.Indexer.Abap.Statements.Statements;
using Comment = ScipSharp.Indexer.Abap.Statements.Statements.Comment;

internal sealed class StatementMap
{
    private sealed class Entry(IStatement statement)
    {
        public readonly IStatement Statement = statement;
        public IStatementRunnable? Matcher;
    }

    private static readonly IReadOnlyList<(IStatement Statement, IStatementRunnable Matcher)> EMPTY = [];

    // this also serves as container for statement matcher singletons
    private readonly Dictionary<string, List<Entry>> map = [];
    private readonly Dictionary<string, IReadOnlyList<(IStatement, IStatementRunnable)>> resolved = [];
    private readonly object sync = new();

    public StatementMap()
    {
        foreach (var stat in ArtifactsABAP.GetStatements())
        {
            var f = stat.GetMatcher().First();
            if (f.Count == 0)
            {
                throw new InvalidOperationException("StatementMap, first must have contents");
            }
            foreach (var first in f)
            {
                if (map.TryGetValue(first, out var list))
                {
                    list.Add(new Entry(stat));
                }
                else
                {
                    map[first] = [new Entry(stat)];
                }
            }
        }
    }

    public IReadOnlyList<(IStatement Statement, IStatementRunnable Matcher)> Lookup(string str)
    {
        var key = JsString.ToUpper(str);
        lock (sync)
        {
            if (resolved.TryGetValue(key, out var done))
            {
                return done;
            }
            if (!map.TryGetValue(key, out var res))
            {
                return EMPTY;
            }
            foreach (var r in res)
            {
                r.Matcher ??= r.Statement.GetMatcher();
            }
            var ret = res.Select(r => (r.Statement, r.Matcher!)).ToList();
            resolved[key] = ret;
            return ret;
        }
    }
}

internal sealed class WorkArea
{
    public readonly IFile File;
    public readonly IReadOnlyList<AbstractToken> Tokens;
    public List<StatementNode> Statements;

    public WorkArea(IFile file, IReadOnlyList<AbstractToken> tokens)
    {
        File = file;
        Tokens = tokens;
        Statements = [];
    }

    public void AddUnknown(List<AbstractToken> pre, List<AbstractToken> post, AbstractToken? colon)
    {
        var st = new StatementNode(new Unknown(), colon);
        st.SetChildren(TokensToNodes(pre, post));
        Statements.Add(st);
    }

    public IStatementResult ToResult() => new(File, Tokens, Statements);

    private static List<IExpressionOrTokenNode> TokensToNodes(List<AbstractToken> tokens1, List<AbstractToken> tokens2)
    {
        var ret = new List<IExpressionOrTokenNode>(tokens1.Count + tokens2.Count);

        foreach (var t in tokens1)
        {
            ret.Add(new TokenNode(t));
        }
        foreach (var t in tokens2)
        {
            ret.Add(new TokenNode(t));
        }

        return ret;
    }
}

public sealed class StatementParser
{
    public const int STATEMENT_MAX_TOKENS = 20000;

    /// <summary>How many line splits <see cref="LazyUnknown"/> tries on one unknown statement.</summary>
    private const int LAZY_UNKNOWN_MAX_SPLITS = 50;

    private static readonly AbstractToken[] EMPTY_PRAGMAS = [];

    private static readonly Lazy<StatementMap> map = new(() => new StatementMap(), LazyThreadSafetyMode.ExecutionAndPublication);
    private readonly ABAPRelease release;
    private readonly LanguageVersion languageVersion;
    private readonly IRegistry? reg;

    public StatementParser(ABAPRelease release, IRegistry? reg = null, LanguageVersion languageVersion = LanguageVersion.Normal)
    {
        _ = map.Value;
        this.release = release;
        this.languageVersion = languageVersion;
        this.reg = reg;
    }

    /// <summary>Input is one full object.</summary>
    public List<IStatementResult> Run(IReadOnlyList<IABAPLexerResult> input, IReadOnlyList<string> globalMacros)
    {
        var macros = new ExpandMacros(globalMacros, release, reg, languageVersion);

        var wa = input.Select(i => new WorkArea(i.File, i.Tokens)).ToList();

        foreach (var w in wa)
        {
            Process(w);
            Categorize(w);
            macros.Find(w.Statements, w.File);
        }

        foreach (var w in wa)
        {
            var res = macros.HandleMacros(w.Statements, w.File);
            w.Statements = res.Statements;
            if (res.ContainsUnknown)
            {
                LazyUnknown(w);
            }
            NativeSQL(w);
        }

        return wa.Select(w => w.ToResult()).ToList();
    }

    // todo, refactor, remove method here and only have in WorkArea class
    private static List<IExpressionOrTokenNode> TokensToNodes(IEnumerable<AbstractToken> tokens)
    {
        var ret = new List<IExpressionOrTokenNode>();

        foreach (var t in tokens)
        {
            ret.Add(new TokenNode(t));
        }

        return ret;
    }

    // tries to split Unknown statements by newlines, when adding/writing a new statement
    // in an editor, adding the statement terminator is typically the last thing to do
    // note: this will not work if the second statement is a macro call, guess this is okay
    private void LazyUnknown(WorkArea wa)
    {
        var result = new List<StatementNode>();

        foreach (var s in wa.Statements)
        {
            var statement = s;
            // dont use CALL METHOD, when executing lazy, it easily gives a Move for the last statement if lazy logic is evaluated
            if (statement.Get() is Unknown)
            {
                var concat = JsString.ToUpper(statement.ConcatTokens());
                if (!concat.StartsWith("CALL METHOD ", StringComparison.Ordinal)
                    && !concat.StartsWith("RAISE EXCEPTION TYPE ", StringComparison.Ordinal)
                    && !concat.StartsWith("READ TABLE ", StringComparison.Ordinal)
                    && !concat.StartsWith("LOOP AT ", StringComparison.Ordinal)
                    && !concat.StartsWith("SELECT SINGLE ", StringComparison.Ordinal)
                    && !concat.StartsWith("CALL FUNCTION ", StringComparison.Ordinal))
                {
                    var tokens = statement.GetTokens();
                    var attempts = 0;
                    foreach (var split in BuildSplits(tokens))
                    {
                        if (tokens.Count - split == 1)
                        {
                            continue; // probably punctuation
                        }
                        // Not in abaplint: every attempt parses the rest of the statement, so an unknown statement of
                        // n lines cost n parses of up to n lines. Recovering from a missing period only needs the first
                        // few splits, so the attempts are capped.
                        if (++attempts > LAZY_UNKNOWN_MAX_SPLITS)
                        {
                            break;
                        }
                        var st = CategorizeStatement(new StatementNode(new Unknown()).SetChildren(TokensToNodes(tokens.Skip(split))));
                        if (st.Get() is not Unknown && st.Get() is not Empty)
                        {
                            result.Add(new StatementNode(new Unknown()).SetChildren(TokensToNodes(tokens.Take(split))));
                            statement = st;
                            break;
                        }
                    }
                }
            }
            result.Add(statement);
        }

        wa.Statements = result;
    }

    /// <summary>The token indexes at which a new line starts: the statement is split into [0, i) and [i, end).</summary>
    private static IEnumerable<int> BuildSplits(IReadOnlyList<AbstractToken> tokens)
    {
        var prevRow = tokens[0].GetRow();

        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].GetRow() != prevRow)
            {
                yield return i;
            }
            prevRow = tokens[i].GetRow();
        }
    }

    private static void NativeSQL(WorkArea wa)
    {
        var sql = false;

        for (var i = 0; i < wa.Statements.Count; i++)
        {
            var statement = wa.Statements[i];
            var type = statement.Get();
            if (type is ExecSQL
                || (type is MethodImplementation && statement.FindDirectExpression<E.Language>() != null))
            {
                sql = true;
            }
            else if (sql)
            {
                if (type is EndExec || type is EndMethod)
                {
                    sql = false;
                }
                else
                {
                    wa.Statements[i] = new StatementNode(new S.NativeSQL()).SetChildren(TokensToNodes(statement.GetTokens()));

                    if (JsString.ToUpper(statement.ConcatTokens()).EndsWith("ENDMETHOD.", StringComparison.Ordinal))
                    {
                        var tokens = statement.GetTokens();
                        var startTokens = TokensToNodes(tokens.Skip(tokens.Count - 2));
                        var endTokens = TokensToNodes(tokens.Take(tokens.Count - 2));
                        wa.Statements[i] = new StatementNode(new S.NativeSQL()).SetChildren(endTokens);
                        var item = new StatementNode(new EndMethod()).SetChildren(startTokens);
                        wa.Statements.Insert(i + 1, item);
                        sql = false;
                    }
                }
            }
        }
    }

    // for each statement, run statement matchers to figure out which kind of statement it is
    private void Categorize(WorkArea wa)
    {
        var result = new List<StatementNode>(wa.Statements.Count);

        foreach (var statement in wa.Statements)
        {
            result.Add(CategorizeStatement(statement));
        }
        wa.Statements = result;
    }

    private StatementNode CategorizeStatement(StatementNode input)
    {
        var statement = input;

        var length = input.GetChildren().Count;
        var lastToken = input.GetLastToken();
        var isPunctuation = lastToken is Punctuation;

        if (length == 1 && isPunctuation)
        {
            var tokens = statement.GetTokens();
            statement = new StatementNode(new Empty()).SetChildren(TokensToNodes(tokens));
        }
        else if (statement.Get() is Unknown)
        {
            if (isPunctuation)
            {
                statement = Match(statement);
            }
            else if (length > STATEMENT_MAX_TOKENS)
            {
                // if the statement contains more than STATEMENT_MAX_TOKENS tokens, just give up
                statement = input;
            }
            else if (length == 1 && lastToken is Pragma)
            {
                // special case, everything crashes if StatementNodes doesnt have children
                statement = new StatementNode(new Empty(), null, [lastToken])
                    .SetChildren(TokensToNodes([lastToken]));
            }
        }

        return statement;
    }

    private static (List<AbstractToken> Tokens, IReadOnlyList<AbstractToken> Pragmas) RemovePragma(IReadOnlyList<AbstractToken> tokens)
    {
        var hasPragma = false;
        // skip the last token as it is the punctuation
        for (var i = 0; i < tokens.Count - 1; i++)
        {
            if (tokens[i] is Pragma)
            {
                hasPragma = true;
                break;
            }
        }
        if (!hasPragma)
        {
            return (tokens.Take(Math.Max(0, tokens.Count - 1)).ToList(), EMPTY_PRAGMAS);
        }

        var result = new List<AbstractToken>();
        var pragmas = new List<AbstractToken>();
        for (var i = 0; i < tokens.Count - 1; i++)
        {
            var t = tokens[i];
            if (t is Pragma)
            {
                pragmas.Add(t);
            }
            else
            {
                result.Add(t);
            }
        }

        return (result, pragmas);
    }

    // ---- not in abaplint: which statement the parser is on, for a run that is watched from another thread ----

    /// <summary>The tokens of the statement being matched right now; null between statements.</summary>
    public static volatile IReadOnlyList<AbstractToken>? Current;

    /// <summary>Statements that took more than two seconds to match: the time in milliseconds and the shape (<see cref="Shape"/>). At most fifty.</summary>
    public static readonly System.Collections.Concurrent.ConcurrentQueue<(long Milliseconds, string Shape)> Slow = new();

    private static readonly Lazy<HashSet<string>> Keywords = new(() => ArtifactsABAP.GetStatements()
        .SelectMany(s => s.GetMatcher().ListKeywords()).SelectMany(k => k.Split(' ', '-'))
        .Select(k => k.ToUpperInvariant()).Where(k => k.Length > 0).ToHashSet(StringComparer.Ordinal));

    /// <summary>
    /// A statement with everything that is not the language taken out: a keyword and a sign stay, a name is <c>x</c>, a
    /// literal <c>'s'</c> or <c>1</c>. It says what kind of statement it is and can be shown to anybody.
    /// </summary>
    public static string Shape(IReadOnlyList<AbstractToken> tokens, int maxLength = 4000)
    {
        var shape = new System.Text.StringBuilder();
        foreach (var token in tokens)
        {
            var text = token.GetStr();
            string piece;
            if (token is StringToken)
            {
                piece = "'s'";
            }
            else if (token is StringTemplate or StringTemplateBegin or StringTemplateMiddle or StringTemplateEnd)
            {
                piece = "|s|";
            }
            else if (text.Length > 0 && text.All(char.IsDigit))
            {
                piece = "1";
            }
            else if (text.Any(char.IsLetter))
            {
                piece = Keywords.Value.Contains(text.ToUpperInvariant()) ? text.ToUpperInvariant() : "x";
            }
            else
            {
                piece = text;
            }
            shape.Append(piece).Append(' ');
            if (shape.Length > maxLength)
            {
                shape.Append("...");
                break;
            }
        }
        return shape.ToString().TrimEnd();
    }

    private StatementNode Match(StatementNode statement)
    {
        var tokens = statement.GetTokens();
        Current = tokens;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            return MatchInner(statement, tokens);
        }
        finally
        {
            Current = null;
            var elapsed = (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (elapsed > 2000 && Slow.Count < 50)
            {
                Slow.Enqueue((elapsed, Shape(tokens)));
            }
        }
    }

    private StatementNode MatchInner(StatementNode statement, IReadOnlyList<AbstractToken> tokens)
    {
        var (filtered, pragmas) = RemovePragma(tokens);
        if (filtered.Count == 0)
        {
            return new StatementNode(new Empty()).SetChildren(TokensToNodes(tokens));
        }

        foreach (var st in map.Value.Lookup(filtered[0].GetStr()))
        {
            var match = Combi.Run(st.Matcher, filtered, release, languageVersion);
            if (match != null)
            {
                var last = tokens[tokens.Count - 1];
                match.Add(new TokenNode(last));
                var matched = new StatementNode(st.Statement, statement.GetColon(), pragmas).SetChildren(match);
                return SelectReclassify.ReclassifySelect(matched, st.Statement, pragmas);
            }
        }
        // next try the statements without specific keywords
        foreach (var st in map.Value.Lookup(""))
        {
            var match = Combi.Run(st.Matcher, filtered, release, languageVersion);
            if (match != null)
            {
                var last = tokens[tokens.Count - 1];
                match.Add(new TokenNode(last));
                var matched = new StatementNode(st.Statement, statement.GetColon(), pragmas).SetChildren(match);
                return SelectReclassify.ReclassifySelect(matched, st.Statement, pragmas);
            }
        }

        return statement;
    }

    // takes care of splitting tokens into statements, also handles chained statements
    // statements are split by "," or "."
    // additional colons/chaining after the first colon are ignored
    private static void Process(WorkArea wa)
    {
        var add = new List<AbstractToken>();
        var pre = new List<AbstractToken>();
        AbstractToken? colon = null;
        // inside EXEC SQL and an AMDP body a colon reads a host variable, ":lv_a",
        // and is not the chaining marker; recognized here because nativeSQL() runs
        // afterwards and can only re-wrap what this has already cut
        var native = false;

        foreach (var token in wa.Tokens)
        {
            if (token is ScipSharp.Indexer.Abap.Lexer.Tokens.Comment)
            {
                wa.Statements.Add(new StatementNode(new Comment()).SetChildren(TokensToNodes([token])));
                continue;
            }

            add.Add(token);

            var str = token.GetStr();
            if (str.Length == 1)
            {
                if (str == ".")
                {
                    native = NativeAfter(native, add, colon);
                    wa.AddUnknown(pre, add, colon);
                    add = [];
                    pre = [];
                    colon = null;
                }
                else if (str == "," && pre.Count > 0)
                {
                    wa.AddUnknown(pre, add, colon);
                    add = [];
                }
                else if (str == ":" && colon == null && !native)
                {
                    colon = token;
                    add.RemoveAt(add.Count - 1); // do not add colon token to statement
                    pre.AddRange(add);
                    add = [];
                }
                else if (str == ":" && !native)
                {
                    add.RemoveAt(add.Count - 1); // do not add colon token to statement
                }
            }
        }

        if (add.Count > 0)
        {
            wa.AddUnknown(pre, add, colon);
        }
    }

    /// <summary>
    /// Whether the statement that just ended opens or closes a region where a
    /// colon belongs to the statement rather than chaining it.
    /// </summary>
    private static bool NativeAfter(bool native, IReadOnlyList<AbstractToken> tokens, AbstractToken? colon)
    {
        // a pragma sits between the closing keyword and the period, so it is left
        // out before the last word is read: "ENDEXEC ##NEEDED." closes the region
        var words = tokens.Where(t => t is not Pragma).Select(t => JsString.ToUpper(t.GetStr())).ToList();
        string? at(int i) => i >= 0 && i < words.Count ? words[i] : null;
        // a native body usually arrives as one blob terminated by its own closing
        // keyword, so the terminator is the word before the period
        if (at(words.Count - 2) == "ENDEXEC" || at(words.Count - 2) == "ENDMETHOD")
        {
            return false;
        }
        if (colon != null)
        {
            // a fragment of a chain cannot OPEN a region: it is only part of a
            // statement, and taking "DATA: lv_a TYPE i, exec sql." for one would
            // turn chaining off for the rest of the file
            return native;
        }
        if (at(0) == "EXEC" && at(1) == "SQL")
        {
            return true;
        }
        // "BY DATABASE" as well as LANGUAGE, and not LANGUAGE alone: a method
        // may be NAMED language
        if (at(0) == "METHOD"
            && words.Contains("BY")
            && words.Contains("DATABASE")
            && words.Contains("LANGUAGE"))
        {
            return true;
        }
        return native;
    }
}
