// Ported from abaplint packages/core/src/abap/2_statements/expand_macros.ts (MIT, see THIRD-PARTY-NOTICES.md in this project)
using ScipSharp.Indexer.Abap.Lexer.Tokens;
using ScipSharp.Indexer.Abap.Nodes;
using ScipSharp.Indexer.Abap.Statements.Statements;
using ScipSharp.Indexer.Abap.Files;

namespace ScipSharp.Indexer.Abap.Statements;

using E = ScipSharp.Indexer.Abap.Statements.Expressions;
using Comment = ScipSharp.Indexer.Abap.Statements.Statements.Comment;

internal sealed class Macros
{
    private sealed class MacroEntry(List<StatementNode> statements, string? filename)
    {
        public readonly List<StatementNode> Statements = statements;
        public readonly string? Filename = filename;
    }

    // insertion ordered, like the keys of a JS object
    private readonly Dictionary<string, MacroEntry> macros = [];
    private readonly List<string> order = [];

    public Macros(IReadOnlyList<string> globalMacros)
    {
        foreach (var m in globalMacros)
        {
            Set(JsString.ToUpper(m), new MacroEntry([], null));
        }
    }

    private void Set(string key, MacroEntry entry)
    {
        if (!macros.ContainsKey(key))
        {
            order.Add(key);
        }
        macros[key] = entry;
    }

    public void AddMacro(string name, List<StatementNode> contents, string filename)
    {
        if (IsMacro(name))
        {
            return;
        }
        Set(JsString.ToUpper(name), new MacroEntry(contents, filename));
    }

    public List<StatementNode>? GetContents(string name) => macros[JsString.ToUpper(name)].Statements;

    public List<string> ListMacroNames() => new(order);

    public bool IsMacro(string name) => macros.ContainsKey(JsString.ToUpper(name));

    public string? GetMacroFilename(string name) => macros[JsString.ToUpper(name)].Filename;
}

public sealed class ExpandMacros
{
    private readonly Macros macros;
    private readonly IReadOnlyList<string> globalMacros;
    private readonly ABAPRelease release;
    private readonly LanguageVersion languageVersion;
    private readonly IRegistry? reg;

    /// <summary>"reg" must be supplied if there are cross object macros via INCLUDE.</summary>
    public ExpandMacros(IReadOnlyList<string> globalMacros, ABAPRelease release, IRegistry? reg = null,
                        LanguageVersion languageVersion = LanguageVersion.Normal)
    {
        macros = new Macros(globalMacros);
        this.release = release;
        this.languageVersion = languageVersion;
        this.globalMacros = globalMacros;
        this.reg = reg;
    }

    public List<string> ListMacroNames() => macros.ListMacroNames();

    public void Find(List<StatementNode> statements, IFile file, bool clear = true, HashSet<string>? includePath = null)
    {
        includePath ??= [];
        AbstractToken? nameToken = null;
        Position? start = null;
        var contents = new List<StatementNode>();

        var macroReferences = reg?.GetMacroReferences();
        if (clear)
        {
            macroReferences?.Clear(file.GetFilename());
        }

        for (var i = 0; i < statements.Count; i++)
        {
            var statement = statements[i];
            var type = statement.Get();

            if (type is Define)
            {
                // todo, will this break if first token is a pragma?
                nameToken = statement.GetTokens()[1];
                start = statement.GetFirstToken().GetStart();
                contents = [];
            }
            else if (type is Include)
            {
                var includeName = statement.FindDirectExpression<E.IncludeName>()?.ConcatTokens();
                // todo, this does not take function module includes into account
                if (reg?.GetObject("PROG", includeName) is IIncludeProgram prog)
                {
                    prog.Parse(release, globalMacros, reg, languageVersion);
                    var includeMainFile = prog.GetMainABAPFile();
                    // Not in abaplint: an include is walked once per walk, not once per path to it. Includes that include
                    // the same includes (a diamond) were otherwise walked 2^depth times; a second walk adds nothing,
                    // AddMacro keeps the first definition of a name.
                    if (includeMainFile != null && includePath.Add(includeMainFile.GetFilename()))
                    {
                        // slow, this copies everything,
                        Find(includeMainFile.GetStatements().ToList(), includeMainFile, false, includePath);
                    }
                }
            }
            else if (nameToken != null)
            {
                if (type is EndOfDefinition)
                {
                    macros.AddMacro(nameToken.GetStr(), contents, file.GetFilename());
                    macroReferences?.AddDefinition(new IFilenameAndToken(file.GetFilename(), nameToken), start!, statement.GetLastToken().GetEnd());

                    nameToken = null;
                }
                else if (type is not Comment)
                {
                    statements[i] = new StatementNode(new MacroContent()).SetChildren(TokensToNodes(statement.GetTokens()));
                    contents.Add(statements[i]);
                }
            }
        }
    }

    public (List<StatementNode> Statements, bool ContainsUnknown) HandleMacros(IReadOnlyList<StatementNode> statements, IFile file,
                                                                             IReadOnlySet<string>? seen = null)
    {
        seen ??= new HashSet<string>();
        var result = new List<StatementNode>();
        var containsUnknown = false;

        var macroReferences = reg?.GetMacroReferences();

        foreach (var statement in statements)
        {
            var type = statement.Get();
            if (type is Unknown || type is MacroCall)
            {
                var macroName = FindName(statement.GetTokens());
                if (macroName != null && macros.IsMacro(macroName))
                {
                    var filename = macros.GetMacroFilename(macroName);
                    if (filename != null)
                    {
                        macroReferences?.AddReference(new IFilenameAndToken(filename, statement.GetFirstToken()));
                    }

                    result.Add(new StatementNode(new MacroCall(), statement.GetColon()).SetChildren(TokensToNodes(statement.GetTokens())));

                    if (!seen.Contains(JsString.ToUpper(macroName)))
                    {
                        var expanded = ExpandContents(macroName, statement);
                        var nextSeen = new HashSet<string>(seen) { JsString.ToUpper(macroName) };
                        var handled = HandleMacros(expanded, file, nextSeen);
                        result.AddRange(handled.Statements);
                        if (handled.ContainsUnknown)
                        {
                            containsUnknown = true;
                        }
                    }
                    else
                    {
                        var node = new StatementNode(new MacroRecursion(), statement.GetColon());
                        result.Add(node.SetChildren(TokensToNodes(statement.GetTokens())));
                    }

                    continue;
                }
                else
                {
                    containsUnknown = true;
                }
            }
            result.Add(statement);
        }

        return (result, containsUnknown);
    }

    //////////////

    private IReadOnlyList<StatementNode> ExpandContents(string name, StatementNode statement)
    {
        var contents = macros.GetContents(name);
        if (contents == null || contents.Count == 0)
        {
            return [];
        }

        var str = "";
        foreach (var c in contents)
        {
            var concat = c.ConcatTokens();
            if (c.GetTerminator() == ",")
            {
                // workaround for chained statements
                if (concat.EndsWith(','))
                {
                    concat = concat.Substring(0, concat.Length - 1) + ".";
                }
            }
            str += concat + "\n";
        }

        var inputs = BuildInput(statement);
        var i = 1;
        foreach (var input in inputs)
        {
            var search = "&" + i;
            // plain text replacement, "$`" and "$'" in the argument are text, not patterns
            str = str.Replace(search, input, StringComparison.Ordinal);
            i++;
        }

        var memFile = new MemoryFile("expand_macros.abap.prog", str);
        var lexerResult = new Lexer.Lexer().Run(memFile, statement.GetFirstToken().GetStart());

        var result = new StatementParser(release, reg, languageVersion).Run([lexerResult], macros.ListMacroNames());
        return result[0].Statements;
    }

    private static List<string> BuildInput(StatementNode statement)
    {
        var result = new List<string>();
        var tokens = statement.GetTokens();

        var build = "";
        for (var i = 1; i < tokens.Count - 1; i++)
        {
            var now = tokens[i];
            AbstractToken? next = tokens[i + 1];
            if (i + 2 == tokens.Count)
            {
                next = null; // dont take the punctuation
            }

            // argh, macros is a nightmare
            Position end = now.GetStart();
            if (end is VirtualPosition vp)
            {
                end = new VirtualPosition(vp, vp.vrow, vp.vcol + now.GetStr().Length);
            }
            else
            {
                end = now.GetEnd();
            }

            if (next != null && next.GetStart().Equals(end))
            {
                build += now.GetStr();
            }
            else
            {
                build += now.GetStr();
                result.Add(build);
                build = "";
            }
        }

        return result;
    }

    public static string? FindName(IReadOnlyList<AbstractToken> tokens)
    {
        string? macroName = null;
        AbstractToken? previous = null;
        foreach (var i in tokens)
        {
            if (previous != null && previous.GetEnd().GetCol() != i.GetStart().GetCol())
            {
                break;
            }
            else if (i is Identifier || i.GetStr() == "-")
            {
                macroName = macroName == null ? i.GetStr() : macroName + i.GetStr();
            }
            else if (i is Pragma)
            {
                continue;
            }
            else
            {
                break;
            }
            previous = i;
        }
        return macroName;
    }

    private static List<IExpressionOrTokenNode> TokensToNodes(IEnumerable<AbstractToken> tokens)
    {
        var ret = new List<IExpressionOrTokenNode>();

        foreach (var t in tokens)
        {
            ret.Add(new TokenNode(t));
        }

        return ret;
    }
}
