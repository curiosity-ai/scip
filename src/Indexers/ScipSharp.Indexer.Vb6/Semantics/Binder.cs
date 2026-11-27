using System.Text;
using ScipSharp.Indexer.Vb6.Project;
using ScipSharp.Indexer.Vb6.Syntax;
using ScipSharp.Indexer.Vb6.Text;
using Suffix = ScipSharp.SymbolDescriptor.Types.Suffix;

namespace ScipSharp.Indexer.Vb6.Semantics;

/// <summary>Parses every module of a workspace and declares the global symbols (projects, modules, members, controls).</summary>
internal sealed class Binder(Vb6Options options, Action<string>? warn)
{
    /// <summary>External type-library classes and events, by SCIP symbol.</summary>
    public Dictionary<string, Sym> Externals { get; } = new(StringComparer.Ordinal);

    public void Bind(Workspace ws)
    {
        foreach (var project in ws.Projects)
        {
            foreach (var (k, v) in Preprocessor.DefaultConstants)
            {
                project.Constants[k] = v;
            }

            if (project.Vbp is null)
            {
                // Loose module files are usually VBA exports from a VBA7 host (Office 2010+), which also defines VBA6.
                project.Constants["VBA7"] = -1L;
            }

            if (project.Vbp?.CondComp is { } cc)
            {
                Preprocessor.ParseDefinitions(cc, project.Constants);
            }

            foreach (var (k, v) in options.Defines)
            {
                project.Constants[k] = v;
            }

            project.Sym = new Sym(project.Name, SymKind.Project, SymbolNames.Project(project))
            {
                DocumentPath = project.VbpRelativePath,
                Signature = project.Vbp is { } vbp ? $"Name=\"{project.Name}\"" + (vbp.Type is { } t ? $" (Type={t})" : "") : project.Name,
            };
            if (project.Vbp?.Title is { Length: > 0 } title)
            {
                project.Sym.Documentation.Add(title);
            }

            if (project.Vbp?.Description is { Length: > 0 } description)
            {
                project.Sym.Documentation.Add(description);
            }
        }

        foreach (var module in ws.Modules)
        {
            try
            {
                Parse(module);
            }
            catch (IOException e)
            {
                warn?.Invoke($"{module.RelativePath ?? module.AbsolutePath}: {e.Message}");
                module.Source = SourceText.FromString("");
                module.Header = DesignerParser.Parse([]);
                module.Lexed = Lexer.Lex([]);
                module.Syntax = new ModuleSyntax();
            }

            Declare(module);
        }

        foreach (var project in ws.Projects)
        {
            BuildTables(project);
        }
    }

    private void Parse(ModuleInfo module)
    {
        var source = SourceText.Read(module.AbsolutePath, options.Encoding);
        module.Source = source;
        module.Header = DesignerParser.Parse(source.Lines);
        var active = Preprocessor.Evaluate(source.Lines, module.Header.CodeStartLine, module.Owner.Constants);
        module.Lexed = Lexer.Lex(source.Lines, module.Header.CodeStartLine, active);
        module.Syntax = ModuleParser.Parse(module.Lexed.Tokens, source);
    }

    private void Declare(ModuleInfo module)
    {
        var p = module.Owner;
        var syntax = module.Syntax;
        var name = syntax.Attributes.TryGetValue("VB_Name", out var vbName) && vbName.Length > 0 ? vbName
            : module.DeclaredName ?? module.Header.Root?.Name ?? Path.GetFileNameWithoutExtension(module.AbsolutePath);
        var kind = module.Kind switch
        {
            ProjectItemKind.Module => SymKind.StdModule,
            ProjectItemKind.Class => SymKind.Class,
            ProjectItemKind.Form => SymKind.Form,
            ProjectItemKind.UserControl => SymKind.UserControl,
            ProjectItemKind.UserDocument => SymKind.UserDocument,
            ProjectItemKind.PropertyPage => SymKind.PropertyPage,
            _ => SymKind.Designer,
        };
        var moduleSym = new Sym(name, kind, SymbolNames.Module(p, name))
        {
            Module = module,
            DocumentPath = module.RelativePath,
            Parent = p.Sym,
            Signature = ModuleSignature(module, name),
        };
        if (syntax.Attributes.TryGetValue("VB_Description", out var moduleDescription))
        {
            moduleSym.Documentation.Add(moduleDescription);
        }

        if (module.Header.Root is { Caption: { Length: > 0 } caption })
        {
            moduleSym.Documentation.Add($"Caption: {caption}");
        }

        module.Sym = moduleSym;
        if (module.Header.Root is { } root)
        {
            moduleSym.FixedType = ExternalClassOf(root);
        }

        // Designer controls (flat namespace; a control array declares its name several times).
        foreach (var control in module.Header.Controls)
        {
            if (!moduleSym.Members.TryGetValue(control.Name, out var sym) || sym.Kind != SymKind.Control)
            {
                sym = new Sym(control.Name, SymKind.Control, SymbolNames.Member(p, name, control.Name, Suffix.Term))
                {
                    Module = module,
                    Parent = moduleSym,
                    DocumentPath = module.RelativePath,
                    FixedType = ExternalClassOf(control),
                    Signature = $"{control.Name} As {control.QualifiedType}",
                };
                moduleSym.AddMember(sym);
            }

            if (control.Index is not null && !sym.IsArray)
            {
                sym.IsArray = true;
                sym.Signature = $"{control.Name}() As {control.QualifiedType}";
                sym.Documentation.Insert(0, "Control array.");
            }

            if (control.Caption is { Length: > 0 } c && !sym.Documentation.Any(d => d.StartsWith("Caption:", StringComparison.Ordinal)))
            {
                sym.Documentation.Add($"Caption: {c}");
            }

            module.Declared[control] = sym;
        }

        var std = module.IsStandard;
        foreach (var member in syntax.Members)
        {
            var sym = member switch
            {
                ProcedureSyntax proc => DeclareProcedure(module, proc),
                DeclareSyntax decl => NewMember(module, decl, SymKind.Declare, Suffix.Method, decl.IsPublicOr(std)),
                EventSyntax ev => NewMember(module, ev, SymKind.Event, Suffix.Method, true),
                VariableSyntax v => DeclareVariable(module, v),
                UdtSyntax udt => DeclareUdt(module, udt, std),
                EnumSyntax en => DeclareEnum(module, en),
                _ => null,
            };
            if (sym is not null)
            {
                module.Declared[member] = sym;
            }
        }
    }

    private static string ModuleSignature(ModuleInfo module, string name)
    {
        var keyword = module.Kind switch
        {
            ProjectItemKind.Module => "Module",
            ProjectItemKind.Class => "Class",
            ProjectItemKind.Form => module.Header.Root?.TypeName.Equals("MDIForm", StringComparison.OrdinalIgnoreCase) == true ? "MDIForm" : "Form",
            ProjectItemKind.UserControl => "UserControl",
            ProjectItemKind.UserDocument => "UserDocument",
            ProjectItemKind.PropertyPage => "PropertyPage",
            _ => "Designer",
        };
        var b = new StringBuilder($"{keyword} {name}");
        if (module.Header.Root is { Library: not null } root)
        {
            b.Append($" As {root.QualifiedType}");
        }

        foreach (var impl in module.Syntax.Implements)
        {
            b.Append($"\nImplements {impl.Text}");
        }

        return b.ToString();
    }

    private Sym? ExternalClassOf(DesignerObject obj)
    {
        if (obj.Library is null)
        {
            return null;
        }

        return External(obj.Library, obj.TypeName);
    }

    public Sym External(string library, string cls)
    {
        var scip = SymbolNames.ExternalClass(library, cls);
        if (!Externals.TryGetValue(scip, out var sym))
        {
            sym = new Sym(cls, SymKind.ExternalClass, scip) { Signature = $"{library}.{cls}" };
            Externals[scip] = sym;
        }

        return sym;
    }

    public Sym ExternalEvent(Sym cls, string ev)
    {
        var library = cls.Signature[..cls.Signature.LastIndexOf('.')];
        var scip = SymbolNames.ExternalEvent(library, cls.Name, ev);
        if (!Externals.TryGetValue(scip, out var sym))
        {
            sym = new Sym(ev, SymKind.ExternalEvent, scip) { Parent = cls, Signature = $"Event {cls.Signature}.{ev}" };
            Externals[scip] = sym;
        }

        return sym;
    }

    private static Sym DeclareProcedure(ModuleInfo module, ProcedureSyntax proc)
    {
        var isProperty = proc.Kind is ProcedureKind.PropertyGet or ProcedureKind.PropertyLet or ProcedureKind.PropertySet;
        var existing = module.Sym.Members.GetValueOrDefault(proc.Name.Text);
        if (isProperty && existing is { Kind: SymKind.Property })
        {
            // Property Get/Let/Set share one symbol; prefer the Get signature (it carries the type).
            if (proc.Kind == ProcedureKind.PropertyGet)
            {
                existing.Signature = proc.Signature;
                existing.DeclaredType = proc.ReturnType;
            }
            else
            {
                existing.Signature += "\n" + proc.Signature;
            }

            existing.IsPublic |= proc.IsPublicOr(true);
            AddDocs(module, proc, existing);
            return existing;
        }

        var kind = isProperty ? SymKind.Property : proc.Kind == ProcedureKind.Function ? SymKind.Function : SymKind.Sub;
        var sym = NewMember(module, proc, kind, isProperty ? Suffix.Term : Suffix.Method, proc.IsPublicOr(true));
        if (proc.Kind == ProcedureKind.PropertyLet || proc.Kind == ProcedureKind.PropertySet)
        {
            sym.DeclaredType = proc.Parameters.Count > 0 ? proc.Parameters[^1].Type : null;
        }

        return sym;
    }

    private static Sym NewMember(ModuleInfo module, MemberSyntax member, SymKind kind, Suffix suffix, bool isPublic)
    {
        var p = module.Owner;
        var name = member.Name.Text;
        if (module.Sym.Members.TryGetValue(name, out var existing) && existing.Kind == kind)
        {
            return existing; // duplicate declaration (e.g. both #If branches, or invalid code): same symbol
        }

        var sym = new Sym(name, kind, SymbolNames.Member(p, module.Sym.Name, name, suffix))
        {
            Module = module,
            Parent = module.Sym,
            DocumentPath = module.RelativePath,
            IsPublic = isPublic,
            Signature = member.Signature,
            DeclaredType = member switch
            {
                ProcedureSyntax proc => proc.ReturnType,
                DeclareSyntax decl => decl.ReturnType,
                VariableSyntax v => v.Type,
                _ => null,
            },
        };
        AddDocs(module, member, sym);
        module.Sym.AddMember(sym);
        return sym;
    }

    private static Sym DeclareVariable(ModuleInfo module, VariableSyntax v)
    {
        var kind = v.IsConst ? SymKind.Constant : SymKind.Variable;

        // Module-level Dim/Const are Private; Public/Global make them visible.
        var sym = NewMember(module, v, kind, Suffix.Term, v.Visibility is Visibility.Public or Visibility.Global or Visibility.Friend);
        sym.IsArray = v.IsArray;
        sym.IsWithEvents = v.IsWithEvents;
        return sym;
    }

    private static Sym DeclareUdt(ModuleInfo module, UdtSyntax udt, bool std)
    {
        var sym = NewMember(module, udt, SymKind.Udt, Suffix.Type, udt.IsPublicOr(std));
        foreach (var field in udt.Fields)
        {
            if (sym.Members.TryGetValue(field.Name.Text, out var dup))
            {
                module.Declared[field] = dup;
                continue;
            }

            var f = new Sym(field.Name.Text, SymKind.UdtField, SymbolNames.Nested(module.Owner, module.Sym.Name, udt.Name.Text, field.Name.Text))
            {
                Module = module,
                Parent = sym,
                DocumentPath = module.RelativePath,
                Signature = field.Signature,
                DeclaredType = field.Type,
                IsArray = field.IsArray,
            };
            AddDocs(module, field, f);
            sym.AddMember(f);
            module.Declared[field] = f;
        }

        return sym;
    }

    private static Sym DeclareEnum(ModuleInfo module, EnumSyntax en)
    {
        var sym = NewMember(module, en, SymKind.Enum, Suffix.Type, en.Visibility != Visibility.Private);
        foreach (var m in en.Members)
        {
            if (sym.Members.TryGetValue(m.Name.Text, out var dup))
            {
                module.Declared[m] = dup;
                continue;
            }

            var e = new Sym(m.Name.Text, SymKind.EnumMember, SymbolNames.Nested(module.Owner, module.Sym.Name, en.Name.Text, m.Name.Text))
            {
                Module = module,
                Parent = sym,
                DocumentPath = module.RelativePath,
                Signature = m.Signature,
                IsPublic = sym.IsPublic,
            };
            AddDocs(module, m, e);
            sym.AddMember(e);
            module.Sym.Members.TryAdd(e.Name, e);
            module.Declared[m] = e;
        }

        return sym;
    }

    private static void AddDocs(ModuleInfo module, MemberSyntax member, Sym sym)
    {
        if (member.Description is { Length: > 0 } d && !sym.Documentation.Contains(d))
        {
            sym.Documentation.Add(d);
        }

        var doc = Documentation.Above(module.Lexed, member.Start.Line)
            ?? (member is ProcedureSyntax ? null : Documentation.Trailing(module.Lexed, member.Start.Line));
        if (doc is not null && !sym.Documentation.Contains(doc))
        {
            sym.Documentation.Add(doc);
        }
    }

    private static void BuildTables(ProjectInfo p)
    {
        foreach (var module in p.Modules)
        {
            var m = module.Sym;
            p.ModulesByName.TryAdd(m.Name, m);
            var exposed = module.Syntax.Attributes.TryGetValue("VB_Exposed", out var e) && e.Equals("True", StringComparison.OrdinalIgnoreCase);
            if (!module.IsStandard)
            {
                p.GlobalTypes.TryAdd(m.Name, m);
                if (exposed)
                {
                    p.ExposedTypes.TryAdd(m.Name, m);
                }
            }

            foreach (var member in m.Members.Values)
            {
                if (!member.IsPublic || member.Parent != m)
                {
                    continue;
                }

                if (member.Kind is SymKind.Udt or SymKind.Enum)
                {
                    p.GlobalTypes.TryAdd(member.Name, member);
                    if (exposed)
                    {
                        p.ExposedTypes.TryAdd(member.Name, member);
                    }

                    if (member.Kind == SymKind.Enum)
                    {
                        foreach (var em in member.Members.Values)
                        {
                            p.GlobalValues.TryAdd(em.Name, em);
                        }
                    }

                    continue;
                }

                if (module.IsStandard && member.Kind != SymKind.Control)
                {
                    p.GlobalValues.TryAdd(member.Name, member);
                }
            }
        }
    }
}

/// <summary>Documentation comments: the comment block directly above a declaration, or a trailing comment.</summary>
internal static class Documentation
{
    public static string? Above(LexResult lexed, int line)
    {
        var lines = new List<string>();
        for (var l = line - 1; l >= 0 && lexed.CommentLines.TryGetValue(l, out var text); l--)
        {
            lines.Add(text);
        }

        lines.Reverse();
        return Clean(lines);
    }

    public static string? Trailing(LexResult lexed, int line) =>
        lexed.TrailingComments.TryGetValue(line, out var text) ? Clean([text]) : null;

    private static string? Clean(List<string> lines)
    {
        var kept = lines
            .Select(l => l.TrimStart('\'').Trim())
            .Where(l => l.Length > 0 && l.Any(char.IsLetterOrDigit))
            .Select(l => l.TrimStart('-', '=', '*', ' '))
            .ToList();
        return kept.Count == 0 ? null : string.Join('\n', kept);
    }
}
