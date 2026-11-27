using ScipSharp.Indexer.Vb6.Text;

namespace ScipSharp.Indexer.Vb6.Syntax;

/// <summary>
/// Error-tolerant parser for the declarations of a VB6 module. Procedure bodies are kept as statement lists (token runs);
/// the resolver interprets them. Anything it does not understand is skipped statement by statement.
/// </summary>
internal sealed class ModuleParser
{
    private readonly List<Statement> _statements;
    private readonly SourceText? _source;
    private int _pos;

    private ModuleParser(List<Statement> statements, SourceText? source)
    {
        _statements = statements;
        _source = source;
    }

    public static ModuleSyntax Parse(List<Token> tokens, SourceText? source = null)
    {
        var parser = new ModuleParser(SplitStatements(tokens), source);
        return parser.ParseModule();
    }

    /// <summary>Parses the declarators of a <c>Dim</c>/<c>Static</c>/<c>Const</c>/<c>ReDim</c> statement starting at <paramref name="pos"/>.</summary>
    public static List<VariableSyntax> ParseDeclarators(Statement s, int pos, bool isConst, SourceText? source) =>
        new ModuleParser([], source).ParseVariableList(s.Tokens, ref pos, Visibility.Default, s, isConst);

    /// <summary>Splits a token stream at line ends and statement colons; a leading <c>name:</c> becomes its own label statement.</summary>
    public static List<Statement> SplitStatements(List<Token> tokens)
    {
        var result = new List<Statement>();
        var current = new List<Token>();
        var lineStart = true;
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Kind == TokenKind.NewLine)
            {
                Flush();
                lineStart = true;
                continue;
            }

            if (t.IsPunct(":"))
            {
                Flush();
                lineStart = false;
                continue;
            }

            if (lineStart && current.Count == 0 && (t.IsName || t.Kind == TokenKind.Number)
                && i + 1 < tokens.Count && tokens[i + 1].IsPunct(":"))
            {
                result.Add(new Statement([t, tokens[i + 1]]));
                i++;
                lineStart = false;
                continue;
            }

            lineStart = false;
            current.Add(t);
        }

        Flush();
        return result;

        void Flush()
        {
            if (current.Count > 0)
            {
                result.Add(new Statement([.. current]));
                current.Clear();
            }
        }
    }

    private ModuleSyntax ParseModule()
    {
        var module = new ModuleSyntax();
        while (_pos < _statements.Count)
        {
            var s = _statements[_pos++];
            try
            {
                ParseModuleStatement(module, s);
            }
            catch (IndexOutOfRangeException)
            {
                // Truncated declaration: keep what we have and continue with the next statement.
            }
            catch (ArgumentOutOfRangeException)
            {
            }
        }

        return module;
    }

    private void ParseModuleStatement(ModuleSyntax module, Statement s)
    {
        var t = s.Tokens;
        if (s.IsLabel)
        {
            return;
        }

        if (t[0].Is("Attribute"))
        {
            ParseAttribute(module, s);
            return;
        }

        if (t[0].Is("Option"))
        {
            if (t.Length > 1 && t[1].Is("Explicit"))
            {
                module.OptionExplicit = true;
            }

            return;
        }

        if (t[0].Is("Implements"))
        {
            var i = 1;
            if (ParseTypeName(t, ref i) is { } impl)
            {
                module.Implements.Add(impl);
            }

            return;
        }

        var pos = 0;
        var visibility = Visibility.Default;
        var isStatic = false;
        var sawDim = false;
        while (pos < t.Length)
        {
            if (t[pos].Is("Public"))
            {
                visibility = Visibility.Public;
            }
            else if (t[pos].Is("Private"))
            {
                visibility = Visibility.Private;
            }
            else if (t[pos].Is("Friend"))
            {
                visibility = Visibility.Friend;
            }
            else if (t[pos].Is("Global"))
            {
                visibility = Visibility.Global;
            }
            else if (t[pos].Is("Static"))
            {
                isStatic = true;
            }
            else if (t[pos].Is("Dim"))
            {
                sawDim = true;
            }
            else
            {
                break;
            }

            pos++;
        }

        if (pos >= t.Length)
        {
            return;
        }

        var head = t[pos];
        if (head.Is("Sub") || head.Is("Function") || head.Is("Property"))
        {
            var proc = ParseProcedureHeader(t, pos, visibility);
            if (proc is null)
            {
                return;
            }

            proc.IsStatic = isStatic;
            proc.Start = t[0];
            proc.Signature = SignatureOf(s);
            ParseBody(proc);
            module.Members.Add(proc);
            return;
        }

        if (head.Is("Declare"))
        {
            if (ParseDeclare(t, pos + 1, visibility) is { } decl)
            {
                decl.Start = t[0];
                decl.End = s.Last;
                decl.Signature = SignatureOf(s);
                module.Members.Add(decl);
            }

            return;
        }

        if (head.Is("Type") && pos + 1 < t.Length && t[pos + 1].Kind == TokenKind.Identifier)
        {
            var udt = new UdtSyntax(t[pos + 1], visibility) { Start = t[0], Signature = SignatureOf(s) };
            ParseBlock("Type", member =>
            {
                var mt = member.Tokens;
                var i = 0;
                foreach (var v in ParseVariableList(mt, ref i, Visibility.Public, member))
                {
                    udt.Fields.Add(v);
                }
            }, end => udt.End = end);
            module.Members.Add(udt);
            return;
        }

        if (head.Is("Enum") && pos + 1 < t.Length && t[pos + 1].Kind == TokenKind.Identifier)
        {
            var en = new EnumSyntax(t[pos + 1], visibility) { Start = t[0], Signature = SignatureOf(s) };
            ParseBlock("Enum", member =>
            {
                var mt = member.Tokens;
                if (mt[0].Kind != TokenKind.Identifier)
                {
                    return;
                }

                var em = new EnumMemberSyntax(mt[0]) { Start = mt[0], End = member.Last, Signature = SignatureOf(member) };
                if (mt.Length > 2 && mt[1].IsPunct("="))
                {
                    em.Value.AddRange(mt[2..]);
                }

                en.Members.Add(em);
            }, end => en.End = end);
            module.Members.Add(en);
            return;
        }

        if (head.Is("Event") && pos + 1 < t.Length && t[pos + 1].Kind == TokenKind.Identifier)
        {
            var ev = new EventSyntax(t[pos + 1], visibility) { Start = t[0], End = s.Last, Signature = SignatureOf(s) };
            var i = pos + 2;
            ParseParameters(t, ref i, ev.Parameters);
            module.Members.Add(ev);
            return;
        }

        if (head.Is("Const"))
        {
            var i = pos + 1;
            foreach (var v in ParseVariableList(t, ref i, visibility, s, isConst: true))
            {
                v.Start = t[0];
                module.Members.Add(v);
            }

            return;
        }

        if (sawDim || visibility != Visibility.Default || isStatic)
        {
            var i = pos;
            foreach (var v in ParseVariableList(t, ref i, visibility, s))
            {
                v.Start = t[0];
                v.IsStatic = isStatic;
                module.Members.Add(v);
            }

            return;
        }

        module.LooseStatements.Add(s);
    }

    private static void ParseAttribute(ModuleSyntax module, Statement s)
    {
        // Attribute VB_Name = "Form1"  |  Attribute m_x.VB_VarHelpID = -1
        var t = s.Tokens;
        if (t.Length >= 4 && t[1].Kind == TokenKind.Identifier && t[2].IsPunct("="))
        {
            var key = t[1].Text;
            var valueToken = t[3];
            var value = valueToken.Kind == TokenKind.String ? valueToken.Text
                : t.Length > 4 && t[3].IsPunct("-") ? "-" + t[4].Text
                : valueToken.Text;
            module.Attributes[key] = value;
            if (key.Equals("VB_Name", StringComparison.OrdinalIgnoreCase) && valueToken.Kind == TokenKind.String)
            {
                module.NameAttribute = valueToken;
            }
        }
    }

    /// <summary>Collects member statements until <c>End &lt;word&gt;</c>.</summary>
    private void ParseBlock(string word, Action<Statement> onMember, Action<Token> onEnd)
    {
        while (_pos < _statements.Count)
        {
            var s = _statements[_pos];
            if (s.StartsWith("End", word))
            {
                _pos++;
                onEnd(s.Last);
                return;
            }

            if (IsProcedureHeader(s) || s.StartsWith("End"))
            {
                return; // unterminated block
            }

            _pos++;
            if (!s.IsLabel)
            {
                try
                {
                    onMember(s);
                }
                catch (IndexOutOfRangeException)
                {
                }
                catch (ArgumentOutOfRangeException)
                {
                }
            }
        }
    }

    public static bool IsProcedureHeader(Statement s)
    {
        foreach (var t in s.Tokens)
        {
            if (t.Is("Public") || t.Is("Private") || t.Is("Friend") || t.Is("Static") || t.Is("Global"))
            {
                continue;
            }

            return t.Is("Sub") || t.Is("Function") || (t.Is("Property") && s.Tokens.Length > 1);
        }

        return false;
    }

    private static ProcedureSyntax? ParseProcedureHeader(Token[] t, int pos, Visibility visibility)
    {
        ProcedureKind kind;
        if (t[pos].Is("Sub"))
        {
            kind = ProcedureKind.Sub;
        }
        else if (t[pos].Is("Function"))
        {
            kind = ProcedureKind.Function;
        }
        else
        {
            pos++;
            if (pos >= t.Length)
            {
                return null;
            }

            if (t[pos].Is("Get"))
            {
                kind = ProcedureKind.PropertyGet;
            }
            else if (t[pos].Is("Let"))
            {
                kind = ProcedureKind.PropertyLet;
            }
            else if (t[pos].Is("Set"))
            {
                kind = ProcedureKind.PropertySet;
            }
            else
            {
                return null;
            }
        }

        pos++;
        if (pos >= t.Length || t[pos].Kind != TokenKind.Identifier)
        {
            return null;
        }

        var proc = new ProcedureSyntax(t[pos], visibility, kind);
        pos++;
        ParseParameters(t, ref pos, proc.Parameters);
        if (pos < t.Length && t[pos].Is("As"))
        {
            pos++;
            proc.ReturnType = ParseTypeRef(t, ref pos);
        }

        return proc;
    }

    private void ParseBody(ProcedureSyntax proc)
    {
        var endWord = proc.Kind switch
        {
            ProcedureKind.Sub => "Sub",
            ProcedureKind.Function => "Function",
            _ => "Property",
        };
        while (_pos < _statements.Count)
        {
            var s = _statements[_pos];
            if (s.StartsWith("End", endWord))
            {
                _pos++;
                proc.End = s.Last;
                proc.Closed = true;
                return;
            }

            if (IsProcedureHeader(s))
            {
                break; // missing End: the next procedure starts here
            }

            _pos++;
            if (s.Tokens.Length >= 4 && s.First.Is("Attribute") && s.Tokens[1].Kind == TokenKind.Identifier
                && s.Tokens[2].IsPunct(".") && s.Tokens[3].Is("VB_Description"))
            {
                var value = s.Tokens.FirstOrDefault(x => x.Kind == TokenKind.String);
                if (value is not null)
                {
                    proc.Description = value.Text;
                }

                continue;
            }

            proc.Body.Add(s);
        }

        proc.End = proc.Body.Count > 0 ? proc.Body[^1].Last : proc.Name;
    }

    private static DeclareSyntax? ParseDeclare(Token[] t, int pos, Visibility visibility)
    {
        if (pos < t.Length && t[pos].Is("PtrSafe"))
        {
            pos++;
        }

        if (pos + 1 >= t.Length || !(t[pos].Is("Sub") || t[pos].Is("Function")))
        {
            return null;
        }

        var decl = new DeclareSyntax(t[pos + 1], visibility, t[pos].Is("Function"));
        pos += 2;
        if (pos < t.Length && t[pos].Is("Lib") && pos + 1 < t.Length)
        {
            decl.Library = t[pos + 1].Text;
            pos += 2;
        }

        if (pos < t.Length && t[pos].Is("Alias") && pos + 1 < t.Length)
        {
            decl.Alias = t[pos + 1].Text;
            pos += 2;
        }

        ParseParameters(t, ref pos, decl.Parameters);
        if (pos < t.Length && t[pos].Is("As"))
        {
            pos++;
            decl.ReturnType = ParseTypeRef(t, ref pos);
        }

        return decl;
    }

    /// <summary>Parses <c>(params)</c> if present at <paramref name="pos"/>.</summary>
    public static void ParseParameters(Token[] t, ref int pos, List<ParameterSyntax> into)
    {
        if (pos >= t.Length || !t[pos].IsPunct("("))
        {
            return;
        }

        var close = MatchParen(t, pos);
        var i = pos + 1;
        while (i < close)
        {
            var end = i;
            var depth = 0;
            while (end < close && !(depth == 0 && t[end].IsPunct(",")))
            {
                if (t[end].IsPunct("("))
                {
                    depth++;
                }
                else if (t[end].IsPunct(")"))
                {
                    depth--;
                }

                end++;
            }

            if (ParseParameter(t[i..end]) is { } p)
            {
                into.Add(p);
            }

            i = end + 1;
        }

        pos = close + 1;
    }

    private static ParameterSyntax? ParseParameter(Token[] t)
    {
        var i = 0;
        bool optional = false, byVal = false, paramArray = false;
        while (i < t.Length)
        {
            if (t[i].Is("Optional"))
            {
                optional = true;
            }
            else if (t[i].Is("ByVal"))
            {
                byVal = true;
            }
            else if (t[i].Is("ByRef"))
            {
                byVal = false;
            }
            else if (t[i].Is("ParamArray"))
            {
                paramArray = true;
            }
            else
            {
                break;
            }

            i++;
        }

        if (i >= t.Length || t[i].Kind != TokenKind.Identifier)
        {
            return null;
        }

        var name = t[i++];
        var isArray = false;
        if (i + 1 < t.Length && t[i].IsPunct("(") && t[i + 1].IsPunct(")"))
        {
            isArray = true;
            i += 2;
        }

        TypeRefSyntax? type = null;
        if (i < t.Length && t[i].Is("As"))
        {
            i++;
            type = ParseTypeRef(t, ref i);
        }

        var def = new List<Token>();
        if (i < t.Length && t[i].IsPunct("="))
        {
            def.AddRange(t[(i + 1)..]);
        }

        return new ParameterSyntax(name, type, def, optional, byVal, paramArray, isArray);
    }

    /// <summary>Parses <c>[New] A.B [* n]</c> after <c>As</c>.</summary>
    public static TypeRefSyntax? ParseTypeRef(Token[] t, ref int pos)
    {
        var isNew = false;
        if (pos < t.Length && t[pos].Is("New"))
        {
            isNew = true;
            pos++;
        }

        var name = ParseTypeName(t, ref pos);
        if (name is null)
        {
            return null;
        }

        var fixedLength = new List<Token>();
        if (pos < t.Length && t[pos].IsPunct("*"))
        {
            pos++;
            while (pos < t.Length && !t[pos].IsPunct(",") && !t[pos].IsPunct(")") && !t[pos].IsPunct("="))
            {
                fixedLength.Add(t[pos++]);
            }
        }

        return name with { IsNew = isNew, FixedLength = fixedLength };
    }

    public static TypeRefSyntax? ParseTypeName(Token[] t, ref int pos)
    {
        if (pos >= t.Length || t[pos].Kind != TokenKind.Identifier)
        {
            return null;
        }

        var parts = new List<Token> { t[pos++] };
        while (pos + 1 < t.Length && t[pos].IsPunct(".") && t[pos + 1].Kind == TokenKind.Identifier)
        {
            parts.Add(t[pos + 1]);
            pos += 2;
        }

        return new TypeRefSyntax(parts, false, []);
    }

    /// <summary><c>[WithEvents] a[(bounds)] [As T], b = 1, ...</c> (constants when <paramref name="isConst"/>).</summary>
    private List<VariableSyntax> ParseVariableList(Token[] t, ref int pos, Visibility visibility, Statement statement, bool isConst = false)
    {
        var result = new List<VariableSyntax>();
        while (pos < t.Length)
        {
            var withEvents = false;
            var declStart = pos;
            if (t[pos].Is("WithEvents"))
            {
                withEvents = true;
                pos++;
            }

            if (pos >= t.Length || t[pos].Kind != TokenKind.Identifier)
            {
                break;
            }

            var v = new VariableSyntax(t[pos], visibility) { IsConst = isConst, IsWithEvents = withEvents };
            var start = pos;
            pos++;
            if (pos < t.Length && t[pos].IsPunct("("))
            {
                var close = MatchParen(t, pos);
                v.IsArray = true;
                if (close > pos + 1)
                {
                    v.Expressions.Add([.. t[(pos + 1)..close]]);
                }

                pos = close + 1;
            }

            if (pos < t.Length && t[pos].Is("As"))
            {
                pos++;
                v.Type = ParseTypeRef(t, ref pos);
            }

            if (pos < t.Length && t[pos].IsPunct("="))
            {
                pos++;
                var exprStart = pos;
                var depth = 0;
                while (pos < t.Length && !(depth == 0 && t[pos].IsPunct(",")))
                {
                    if (t[pos].IsPunct("("))
                    {
                        depth++;
                    }
                    else if (t[pos].IsPunct(")"))
                    {
                        depth--;
                    }

                    pos++;
                }

                v.Expressions.Add([.. t[exprStart..pos]]);
            }

            // Skip anything unexpected up to the next comma.
            while (pos < t.Length && !t[pos].IsPunct(","))
            {
                pos++;
            }

            v.Start = t[start];
            v.End = t[Math.Max(start, pos - 1)];
            v.Signature = _source is null ? "" : SignatureFor(statement, result.Count == 0 ? statement.First : t[declStart], t[Math.Max(start, pos - 1)], isConst, visibility);
            result.Add(v);
            if (pos < t.Length && t[pos].IsPunct(","))
            {
                pos++;
            }
        }

        return result;
    }

    private string SignatureFor(Statement statement, Token first, Token last, bool isConst, Visibility visibility)
    {
        var text = _source!.Slice(first.Line, first.Col - (first.Bracketed ? 1 : 0), last.Line, last.EndCol + (last.Bracketed ? 1 : 0) + (last.Suffix != '\0' ? 1 : 0));
        var lead = statement.First;
        if (ReferenceEquals(lead, first))
        {
            return text;
        }

        var prefix = isConst ? "Const"
            : visibility switch
            {
                Visibility.Public => "Public",
                Visibility.Private => "Private",
                Visibility.Global => "Global",
                Visibility.Friend => "Friend",
                _ => lead.Is("Static") ? "Static" : lead.Is("Dim") ? "Dim" : "",
            };
        if (isConst && visibility is Visibility.Public or Visibility.Private or Visibility.Global)
        {
            prefix = $"{visibility} Const";
        }

        return prefix.Length > 0 ? $"{prefix} {text}" : text;
    }

    private string SignatureOf(Statement s)
    {
        if (_source is null)
        {
            return "";
        }

        var first = s.First;
        var last = s.Last;
        return _source.Slice(first.Line, first.Col - (first.Bracketed ? 1 : 0), last.Line, EndColOf(last));
    }

    public static int EndColOf(Token t) => t.Kind switch
    {
        TokenKind.Identifier => t.EndCol + (t.Bracketed ? 1 : 0) + (t.Suffix != '\0' ? 1 : 0),
        _ => t.EndCol,
    };

    public static int MatchParen(Token[] t, int open)
    {
        var depth = 0;
        for (var i = open; i < t.Length; i++)
        {
            if (t[i].IsPunct("("))
            {
                depth++;
            }
            else if (t[i].IsPunct(")"))
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return t.Length;
    }
}
