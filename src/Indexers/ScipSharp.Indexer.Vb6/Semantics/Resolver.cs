using ScipSharp.Indexer.Vb6.Indexing;
using ScipSharp.Indexer.Vb6.Syntax;

namespace ScipSharp.Indexer.Vb6.Semantics;

/// <summary>Workspace-wide lookup state shared by the per-module resolvers.</summary>
internal sealed class ResolverContext(Binder binder, IEnumerable<ProjectInfo> projects)
{
    public Binder Binder { get; } = binder;

    public Dictionary<Sym, ProjectInfo> ProjectsBySym { get; } = projects.ToDictionary(p => p.Sym);
}

/// <summary>
/// Resolves the names of one module and records definitions and references. Scoping follows VB6: procedure locals and
/// parameters, then the module (members, controls, enum members), then the project (Public members of standard modules,
/// Public enums/UDTs, module names), then referenced projects (exposed classes). Member access (<c>a.b</c>) is typed
/// best-effort through declared types (<c>As</c>), function return types, <c>Me</c>, <c>New</c> and <c>With</c> blocks.
/// Names that do not resolve (VB runtime functions, members of COM objects) produce no occurrence.
/// </summary>
internal sealed class Resolver(ResolverContext context, ModuleInfo module, DocumentBuilder doc)
{
    private const int Write = (int)SymbolRole.WriteAccess;

    private static readonly string[] DesignerEventPrefixes = ["Form", "MDIForm", "UserControl", "UserDocument", "PropertyPage"];

    private readonly ProjectInfo _project = module.Owner;
    private readonly List<Sym> _defined = [];
    private Dictionary<string, Sym>? _locals;
    private Dictionary<string, Sym>? _labels;
    private Sym? _scopeOwner;
    private readonly Stack<Sym?> _with = new();

    public void Run()
    {
        var m = module.Sym;
        var lines = module.Source.Lines;
        var fileRange = new Range(0, 0, Math.Max(0, lines.Count - 1), lines.Count == 0 ? 0 : lines[^1].Length);
        var nameAttr = module.Syntax.NameAttribute;
        if (nameAttr is not null && nameAttr.EndCol - nameAttr.Col >= 2)
        {
            doc.Definition(m, new Span(nameAttr.Line, nameAttr.Col + 1, nameAttr.EndCol - 1), fileRange);
        }

        Define(m);
        if (module.Header.Root is { } root)
        {
            if (nameAttr is null)
            {
                doc.Definition(m, root.NameSpan, fileRange);
            }
            else
            {
                doc.Reference(m, root.NameSpan);
            }

            if (root.TypeSpan is { } ts && m.FixedType is { } rootType)
            {
                doc.Reference(rootType, ts);
            }
        }

        foreach (var control in module.Header.Controls)
        {
            var sym = module.Declared[control];
            doc.Definition(sym, control.NameSpan);
            Define(sym);
            if (control.TypeSpan is { } ts && sym.FixedType is { } type)
            {
                doc.Reference(type, ts);
            }
        }

        foreach (var impl in module.Syntax.Implements)
        {
            if (ResolveTypeRef(impl, module, emit: true) is { } iface && iface != m)
            {
                AddRelationship(m, iface, implementation: true);
            }
        }

        foreach (var member in module.Syntax.Members)
        {
            try
            {
                ResolveMember(member);
            }
            catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException or KeyNotFoundException)
            {
                // Error tolerance: a malformed member must not lose the rest of the document.
            }
            finally
            {
                _locals = null;
                _labels = null;
                _scopeOwner = null;
                _with.Clear();
            }
        }

        foreach (var s in module.Syntax.LooseStatements)
        {
            ResolveStatement(s);
        }

        foreach (var sym in _defined)
        {
            doc.Define(sym);
        }
    }

    private void Define(Sym sym) => _defined.Add(sym);

    private static Range EnclosingOf(MemberSyntax member) =>
        new(member.Start.Line, member.Start.Col - (member.Start.Bracketed ? 1 : 0), member.End.Line, ModuleParser.EndColOf(member.End));

    private void ResolveMember(MemberSyntax member)
    {
        var sym = module.Declared[member];
        doc.Definition(sym, member.Name, EnclosingOf(member));
        Define(sym);
        switch (member)
        {
            case ProcedureSyntax proc:
                ResolveProcedure(proc, sym);
                break;
            case DeclareSyntax decl:
                OpenScope(sym);
                DeclareParameters(decl.Parameters, sym);
                ResolveTypeRef(decl.ReturnType, module, emit: true);
                break;
            case EventSyntax ev:
                OpenScope(sym);
                DeclareParameters(ev.Parameters, sym);
                break;
            case VariableSyntax v:
                ResolveTypeRef(v.Type, module, emit: true);
                foreach (var e in v.Expressions)
                {
                    ResolveTokens([.. e]);
                }

                break;
            case UdtSyntax udt:
                foreach (var field in udt.Fields)
                {
                    var f = module.Declared[field];
                    doc.Definition(f, field.Name, EnclosingOf(field));
                    Define(f);
                    ResolveTypeRef(field.Type, module, emit: true);
                    foreach (var e in field.Expressions)
                    {
                        ResolveTokens([.. e]);
                    }
                }

                break;
            case EnumSyntax en:
                foreach (var em in en.Members)
                {
                    var e = module.Declared[em];
                    doc.Definition(e, em.Name, EnclosingOf(em));
                    Define(e);
                    if (em.Value.Count > 0)
                    {
                        ResolveTokens([.. em.Value]);
                    }
                }

                break;
        }
    }

    private void OpenScope(Sym owner)
    {
        _locals = new Dictionary<string, Sym>(StringComparer.OrdinalIgnoreCase);
        _labels = new Dictionary<string, Sym>(StringComparer.OrdinalIgnoreCase);
        _scopeOwner = owner;
        _with.Clear();
    }

    private void DeclareParameters(List<ParameterSyntax> parameters, Sym owner)
    {
        foreach (var p in parameters)
        {
            var sig = (p.IsOptional ? "Optional " : "") + (p.IsByVal ? "ByVal " : "") + (p.IsParamArray ? "ParamArray " : "")
                + p.Name.Text + (p.IsArray ? "()" : "") + (p.Type is { } t ? $" As {(t.IsNew ? "New " : "")}{t.Text}" : "");
            var local = new Sym(p.Name.Text, SymKind.Parameter, doc.NewLocal())
            {
                Module = module,
                Parent = owner,
                DeclaredType = p.Type,
                IsArray = p.IsArray,
                Signature = sig,
            };
            _locals!.TryAdd(local.Name, local);
            doc.Definition(local, p.Name);
            Define(local);
            ResolveTypeRef(p.Type, module, emit: true);
            if (p.Default.Count > 0)
            {
                ResolveTokens([.. p.Default]);
            }
        }
    }

    private void ResolveProcedure(ProcedureSyntax proc, Sym sym)
    {
        OpenScope(sym);
        DeclareParameters(proc.Parameters, sym);
        ResolveTypeRef(proc.ReturnType, module, emit: true);
        AnalyzeHandlerName(proc, sym);

        // Labels first, so forward GoTo targets resolve.
        foreach (var s in proc.Body)
        {
            if (s.IsLabel && s.First.IsName && !_labels!.ContainsKey(s.First.Text))
            {
                var label = new Sym(s.First.Text, SymKind.Label, doc.NewLocal()) { Module = module, Parent = sym, Signature = s.First.Text + ":" };
                _labels[label.Name] = label;
                doc.Definition(label, s.First);
                Define(label);
            }
        }

        foreach (var s in proc.Body)
        {
            ResolveStatement(s);
        }
    }

    /// <summary>
    /// Event handlers and interface implementations are bound by name: <c>Command1_Click</c> (control),
    /// <c>m_oSocket_OnReceive</c> (WithEvents variable), <c>Form_Load</c> (the designer object), <c>IShape_Draw</c>
    /// (Implements). The prefix gets a reference occurrence and the procedure a relationship to the event or interface member.
    /// </summary>
    private void AnalyzeHandlerName(ProcedureSyntax proc, Sym sym)
    {
        var name = proc.Name.Text;
        for (var k = name.IndexOf('_', 1); k > 0 && k < name.Length - 1; k = name.IndexOf('_', k + 1))
        {
            var prefix = name[..k];
            var suffix = name[(k + 1)..];
            var prefixSpan = new Span(proc.Name.Line, proc.Name.Col, proc.Name.Col + k);

            foreach (var impl in module.Syntax.Implements)
            {
                if (!impl.Parts[^1].Text.Equals(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var iface = ResolveTypeRef(impl, module, emit: false);
                if (iface is null)
                {
                    continue;
                }

                doc.Reference(iface, prefixSpan);
                if (iface.Members.TryGetValue(suffix, out var target) && target.Parent == iface)
                {
                    AddRelationship(sym, target, implementation: true);
                }

                return;
            }

            if (!module.Sym.Members.TryGetValue(prefix, out var source))
            {
                if (module.IsDesigner && DesignerEventPrefixes.Contains(prefix, StringComparer.OrdinalIgnoreCase)
                    && module.Sym.FixedType is { } rootType)
                {
                    AddRelationship(sym, context.Binder.ExternalEvent(rootType, suffix), implementation: false);
                    return;
                }

                continue;
            }

            if (source.Kind == SymKind.Control || (source.Kind == SymKind.Variable && source.IsWithEvents))
            {
                doc.Reference(source, prefixSpan);
                var type = TypeOf(source);
                if (type is { Kind: SymKind.ExternalClass })
                {
                    AddRelationship(sym, context.Binder.ExternalEvent(type, suffix), implementation: false);
                }
                else if (type is not null && type.Members.TryGetValue(suffix, out var ev) && ev.Kind == SymKind.Event)
                {
                    AddRelationship(sym, ev, implementation: false);
                }

                return;
            }
        }
    }

    private void AddRelationship(Sym from, Sym to, bool implementation)
    {
        if (from.Relationships.Any(r => r.Symbol == to.Scip))
        {
            return;
        }

        from.Relationships.Add(new Relationship { Symbol = to.Scip, IsImplementation = implementation, IsReference = !implementation });
        doc.Related(to);
    }

    // ---------------------------------------------------------------- statements

    private void ResolveStatement(Statement s)
    {
        var t = s.Tokens;
        if (s.IsLabel)
        {
            return;
        }

        var head = t[0];
        if (head.Is("Dim") || head.Is("Static") || head.Is("Const") || (head.Is("Private") || head.Is("Public")) && _locals is not null)
        {
            var pos = 1;
            var isConst = head.Is("Const");
            if (pos < t.Length && t[pos].Is("Const"))
            {
                isConst = true;
                pos++;
            }

            DeclareLocals(s, pos, isConst, redim: false);
            return;
        }

        if (head.Is("ReDim"))
        {
            var pos = 1;
            if (pos < t.Length && t[pos].Is("Preserve"))
            {
                pos++;
            }

            DeclareLocals(s, pos, isConst: false, redim: true);
            return;
        }

        if (head.Is("With"))
        {
            var resolved = ResolveTokens(t, 1);
            _with.Push(TypeEndingAt(t, t.Length - 1, 1, resolved));
            return;
        }

        if (head.Is("End") && t.Length > 1 && t[1].Is("With"))
        {
            if (_with.Count > 0)
            {
                _with.Pop();
            }

            return;
        }

        if ((head.Is("GoTo") || head.Is("GoSub") || head.Is("Resume")) && t.Length > 1)
        {
            ReferenceLabel(t[1]);
            return;
        }

        if (head.Is("On"))
        {
            // On Error GoTo label / On Error Resume Next / On x GoTo a, b
            var go = Array.FindIndex(t, x => x.Is("GoTo") || x.Is("GoSub"));
            if (go > 0)
            {
                ResolveTokens(t, 1, go);
                for (var i = go + 1; i < t.Length; i++)
                {
                    ReferenceLabel(t[i]);
                }

                return;
            }
        }

        if (head.Is("RaiseEvent") && t.Length > 1)
        {
            if (module.Sym.Members.TryGetValue(t[1].Text, out var ev) && ev.Kind == SymKind.Event)
            {
                doc.Reference(ev, t[1]);
            }

            ResolveTokens(t, 2);
            return;
        }

        var write = -1;
        var target = head.Is("Set") || head.Is("Let") ? 1 : 0;
        if (head.Is("For"))
        {
            target = t.Length > 1 && t[1].Is("Each") ? 2 : 1;
            write = target < t.Length && t[target].IsName ? target : -1;
        }
        else
        {
            write = AssignmentTarget(t, target);
        }

        ResolveTokens(t, 0, t.Length, write);
    }

    private void ReferenceLabel(Token token)
    {
        if (token.IsName && _labels is not null && _labels.TryGetValue(token.Text, out var label))
        {
            doc.Reference(label, token);
        }
    }

    /// <summary>Index of the identifier assigned by <c>a.b(i).c = ...</c> starting at <paramref name="start"/>, or -1.</summary>
    private static int AssignmentTarget(Token[] t, int start)
    {
        var j = start;
        var last = -1;
        while (j < t.Length)
        {
            if (t[j].Kind == TokenKind.Identifier && (t[j].IsName || j > start))
            {
                last = j;
                j++;
            }
            else if (t[j].IsPunct(".") || t[j].IsPunct("!"))
            {
                j++;
            }
            else if (t[j].IsPunct("("))
            {
                j = ModuleParser.MatchParen(t, j) + 1;
            }
            else
            {
                break;
            }
        }

        return j < t.Length && t[j].IsPunct("=") ? last : -1;
    }

    private void DeclareLocals(Statement s, int pos, bool isConst, bool redim)
    {
        foreach (var v in ModuleParser.ParseDeclarators(s, pos, isConst, module.Source))
        {
            if (_locals is null)
            {
                // Module-level statement that the declaration parser did not take (should not happen); resolve names only.
                ResolveTypeRef(v.Type, module, emit: true);
                continue;
            }

            if (redim && LookupValue(v.Name.Text) is { } existing)
            {
                doc.Reference(existing, v.Name, Write);
            }
            else
            {
                var local = new Sym(v.Name.Text, isConst ? SymKind.LocalConstant : SymKind.LocalVariable, doc.NewLocal())
                {
                    Module = module,
                    Parent = _scopeOwner,
                    DeclaredType = v.Type,
                    IsArray = v.IsArray || redim,
                    Signature = redim && !v.Signature.StartsWith("ReDim", StringComparison.OrdinalIgnoreCase) ? "ReDim " + v.Signature : v.Signature,
                };
                _locals.TryAdd(local.Name, local);
                doc.Definition(local, v.Name, new Range(v.Start.Line, v.Start.Col, v.End.Line, ModuleParser.EndColOf(v.End)));
                Define(local);
            }

            ResolveTypeRef(v.Type, module, emit: true);
            foreach (var e in v.Expressions)
            {
                ResolveTokens([.. e]);
            }
        }
    }

    // ---------------------------------------------------------------- expressions

    private Sym?[] ResolveTokens(Token[] t, int start = 0, int end = -1, int write = -1)
    {
        if (end < 0)
        {
            end = t.Length;
        }

        var resolved = new Sym?[t.Length];
        var typeOfSeen = false;
        for (var i = start; i < end; i++)
        {
            var tok = t[i];
            if (tok.Kind != TokenKind.Identifier)
            {
                continue;
            }

            if (i + 1 < end && t[i + 1].IsPunct(":="))
            {
                continue; // named argument
            }

            var afterDot = i > start && t[i - 1].IsPunct(".");
            if (i > start && t[i - 1].IsPunct("!"))
            {
                continue; // rs!Field: late-bound default member
            }

            Sym? sym;
            if (afterDot)
            {
                var qualifier = QualifierType(t, i - 1, start, resolved);
                sym = Member(qualifier, tok.Text);
            }
            else if (!tok.IsName)
            {
                if (tok.Is("Me"))
                {
                    resolved[i] = module.Sym;
                }
                else if (tok.Is("TypeOf"))
                {
                    typeOfSeen = true;
                }
                else if (tok.Is("New") || (tok.Is("Is") && typeOfSeen))
                {
                    var k = i + 1;
                    if (ModuleParser.ParseTypeName(t, ref k) is { } typeName)
                    {
                        var type = ResolveTypeRef(typeName, module, emit: true);
                        if (k - 1 < resolved.Length)
                        {
                            resolved[k - 1] = type;
                        }

                        i = k - 1;
                    }
                }

                continue;
            }
            else
            {
                sym = LookupValue(tok.Text);
            }

            if (sym is null)
            {
                continue;
            }

            resolved[i] = sym;
            doc.Reference(sym, tok, i == write ? Write : 0);
        }

        return resolved;
    }

    /// <summary>The type an expression ending at the token before a '.' evaluates to (With target for a leading dot).</summary>
    private Sym? QualifierType(Token[] t, int dot, int start, Sym?[] resolved)
    {
        var j = dot - 1;
        if (j < start)
        {
            return _with.Count > 0 ? _with.Peek() : null;
        }

        var prev = t[j];
        if (prev.Kind == TokenKind.Identifier && (prev.IsName || prev.Is("Me") || (j > start && t[j - 1].IsPunct("."))))
        {
            return TypeEndingAt(t, j, start, resolved);
        }

        if (prev.IsPunct(")"))
        {
            return TypeEndingAt(t, j, start, resolved);
        }

        if (prev.Kind == TokenKind.Punct || prev.IsKeyword)
        {
            return _with.Count > 0 ? _with.Peek() : null; // leading dot inside an expression: "If .Visible Then", "x = .Count"
        }

        return null;
    }

    private Sym? TypeEndingAt(Token[] t, int j, int start, Sym?[] resolved)
    {
        if (j < start || j >= t.Length)
        {
            return null;
        }

        if (t[j].IsPunct(")"))
        {
            var depth = 0;
            for (var k = j; k >= start; k--)
            {
                if (t[k].IsPunct(")"))
                {
                    depth++;
                }
                else if (t[k].IsPunct("("))
                {
                    depth--;
                    if (depth == 0)
                    {
                        return k - 1 >= start && t[k - 1].Kind == TokenKind.Identifier && resolved[k - 1] is { } callee
                            ? ValueTypeOf(callee)
                            : null;
                    }
                }
            }

            return null;
        }

        return resolved[j] is { } sym ? TypeOf(sym) : null;
    }

    /// <summary>Type of <c>f(...)</c> / <c>arr(i)</c>: the declared type; for a module/class the default member is unknown.</summary>
    private Sym? ValueTypeOf(Sym sym) => sym.IsType || sym.Kind == SymKind.Project ? null : TypeOf(sym);

    /// <summary>The type through which members of <paramref name="sym"/> are looked up.</summary>
    private Sym? TypeOf(Sym sym)
    {
        if (sym.IsType || sym.Kind == SymKind.Project)
        {
            return sym;
        }

        if (sym.Kind == SymKind.Control)
        {
            return sym.FixedType;
        }

        if (sym.TypeResolved)
        {
            return sym.ResolvedType;
        }

        sym.TypeResolved = true;
        if (sym.Kind is SymKind.Sub or SymKind.Event or SymKind.Label or SymKind.EnumMember)
        {
            return null;
        }

        sym.ResolvedType = ResolveTypeRef(sym.DeclaredType, sym.Module ?? module, emit: false);
        return sym.ResolvedType;
    }

    private Sym? Member(Sym? type, string name)
    {
        if (type is null)
        {
            return null;
        }

        if (type.Kind == SymKind.Project)
        {
            if (!context.ProjectsBySym.TryGetValue(type, out var p))
            {
                return null;
            }

            var own = p == _project;
            return p.ModulesByName.GetValueOrDefault(name)
                ?? (own ? p.GlobalValues.GetValueOrDefault(name) ?? p.GlobalTypes.GetValueOrDefault(name) : p.ExposedTypes.GetValueOrDefault(name));
        }

        return type.Members.GetValueOrDefault(name);
    }

    private Sym? LookupValue(string name)
    {
        if (_locals is not null && _locals.TryGetValue(name, out var local))
        {
            return local;
        }

        if (module.Sym.Members.TryGetValue(name, out var member))
        {
            return member;
        }

        if (_project.GlobalValues.TryGetValue(name, out var global)
            || _project.ModulesByName.TryGetValue(name, out global)
            || _project.GlobalTypes.TryGetValue(name, out global))
        {
            return global;
        }

        if (_project.Vbp is not null && name.Equals(_project.Name, StringComparison.OrdinalIgnoreCase))
        {
            return _project.Sym;
        }

        foreach (var r in _project.References)
        {
            if (name.Equals(r.Name, StringComparison.OrdinalIgnoreCase))
            {
                return r.Sym;
            }

            if (r.ExposedTypes.TryGetValue(name, out var exposed))
            {
                return exposed;
            }
        }

        return null;
    }

    private static Sym? LookupType(string name, ModuleInfo scope)
    {
        if (scope.Sym.Members.TryGetValue(name, out var local) && local.Kind is SymKind.Udt or SymKind.Enum)
        {
            return local;
        }

        var p = scope.Owner;
        if (p.GlobalTypes.TryGetValue(name, out var type))
        {
            return type;
        }

        foreach (var r in p.References)
        {
            if (r.ExposedTypes.TryGetValue(name, out var exposed))
            {
                return exposed;
            }
        }

        return null;
    }

    /// <summary>Resolves <c>As [Lib.]Type</c>; emits references to each resolved part when <paramref name="emit"/>.</summary>
    private Sym? ResolveTypeRef(TypeRefSyntax? tr, ModuleInfo scope, bool emit)
    {
        if (tr is null)
        {
            return null;
        }

        if (emit && tr.FixedLength.Count > 0)
        {
            ResolveTokens([.. tr.FixedLength]);
        }

        if (tr.IsBuiltin)
        {
            return null;
        }

        var parts = tr.Parts;
        if (parts.Count == 1)
        {
            var single = LookupType(parts[0].Text, scope);
            if (single is not null && emit)
            {
                doc.Reference(single, parts[0]);
            }

            return single;
        }

        // Qualified: Project.Type, Module.Type, or TypeLibrary.Class.
        var p = scope.Owner;
        var firstName = parts[0].Text;
        ProjectInfo? qualifiedProject = null;
        if (p.Vbp is not null && firstName.Equals(p.Name, StringComparison.OrdinalIgnoreCase))
        {
            qualifiedProject = p;
        }
        else
        {
            qualifiedProject = p.References.FirstOrDefault(r => firstName.Equals(r.Name, StringComparison.OrdinalIgnoreCase));
        }

        Sym? current = null;
        var index = 1;
        if (qualifiedProject is not null)
        {
            if (emit && qualifiedProject.Vbp is not null)
            {
                doc.Reference(qualifiedProject.Sym, parts[0]);
            }

            var types = qualifiedProject == p ? p.GlobalTypes : qualifiedProject.ExposedTypes;
            current = types.GetValueOrDefault(parts[1].Text) ?? (qualifiedProject == p ? p.ModulesByName.GetValueOrDefault(parts[1].Text) : null);
            if (current is null)
            {
                return null;
            }

            if (emit)
            {
                doc.Reference(current, parts[1]);
            }

            index = 2;
        }
        else if (p.ModulesByName.TryGetValue(firstName, out var mod))
        {
            current = mod;
            if (emit)
            {
                doc.Reference(mod, parts[0]);
            }
        }
        else
        {
            var external = context.Binder.External(string.Join('.', parts.Take(parts.Count - 1).Select(x => x.Text)), parts[^1].Text);
            if (emit)
            {
                doc.Reference(external, parts[^1]);
            }

            return external;
        }

        for (; index < parts.Count; index++)
        {
            if (!current.Members.TryGetValue(parts[index].Text, out var next) || !(next.IsType))
            {
                return null;
            }

            current = next;
            if (emit)
            {
                doc.Reference(current, parts[index]);
            }
        }

        return current;
    }
}
