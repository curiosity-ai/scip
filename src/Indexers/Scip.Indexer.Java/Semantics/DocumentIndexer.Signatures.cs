using System.Text;
using Scip.Indexer.Java.Syntax;

namespace Scip.Indexer.Java.Semantics;

/// <summary>Renders <c>signature_documentation</c> text (port of <c>ScipJavaSignatureFormatter</c>).</summary>
public sealed partial class DocumentIndexer
{
    private const string Unresolved = "unresolved_type";

    private string FormatType(JType t)
    {
        switch (t)
        {
            case PrimT p: return p.Name;
            case ArrayT a: return FormatType(a.Elem) + "[]";
            case VarT v: return v.Name;
            case WildT w:
                return w.Kind switch
                {
                    WildKind.Extends => "? extends " + FormatType(w.Bound!),
                    WildKind.Super => "? super " + FormatType(w.Bound!),
                    _ => "?",
                };
            case ClassT c:
            {
                var simple = ws.Classes.Load(c.Name)?.Simple ?? c.Name[(c.Name.LastIndexOfAny(['/', '$']) + 1)..];
                return c.Args.Count == 0 ? simple : $"{simple}<{string.Join(", ", c.Args.Select(FormatType))}>";
            }
            case NullT: return "null";
            default: return Unresolved;
        }
    }

    private static string FormatAccess(Mod m) => (m & Mod.Private) != 0 ? "private" : (m & Mod.Public) != 0 ? "public" : (m & Mod.Protected) != 0 ? "protected" : "";

    private static string FormatModifiers(Mod m)
    {
        var abs = (m & Mod.Abstract) != 0;
        var def = (m & Mod.Default) != 0;
        if (abs && def) abs = false;
        var parts = new List<string>();
        if (abs) parts.Add("abstract");
        if (def) parts.Add("default");
        if ((m & Mod.Static) != 0) parts.Add("static");
        if ((m & Mod.Final) != 0) parts.Add("final");
        return string.Join(' ', parts);
    }

    private static void Kw(StringBuilder sb, string s)
    {
        if (s.Length > 0) sb.Append(s).Append(' ');
    }

    private string FormatAnnotations(Modifiers? mods)
    {
        if (mods is null || mods.Annotations.Count == 0) return "";
        return string.Join('\n', mods.Annotations.Select(FormatAnnotation));
    }

    private string FormatAnnotation(Annotation a)
    {
        var sb = new StringBuilder("@").Append(a.Name.SimpleName);
        if (a.Args.Count == 1)
        {
            var (name, _, value) = a.Args[0];
            sb.Append('(').Append(name is null or "value" ? FormatExpr(value) : $"{name} = {FormatExpr(value)}").Append(')');
        }
        else if (a.Args.Count > 1)
        {
            sb.Append('(').Append(string.Join(", ", a.Args.Select(x => x.Name is null ? FormatExpr(x.Value) : $"{x.Name} = {FormatExpr(x.Value)}"))).Append(')');
        }
        return sb.ToString();
    }

    private string FormatExpr(Expr e) => e switch
    {
        ArrayInit ai => "{" + string.Join(", ", ai.Items.Select(FormatExpr)) + "}",
        AnnotationExpr ae => FormatAnnotation(ae.A),
        Paren p => FormatExpr(p.E),
        Literal l => FormatLiteral(l),
        Unary { Op: "-" or "+", Postfix: false } u when u.E is Literal => "-" + FormatExpr(u.E),
        Unary { Postfix: false } u => u.Op + FormatExpr(u.E),
        _ => _src[e.Start..e.End],
    };

    private static string FormatLiteral(Literal l)
    {
        switch (l.Kind)
        {
            case Tk.String: return l.Text;
            case Tk.Char: return l.Text;
            case Tk.Number:
            {
                var t = l.Text.Replace("_", "", StringComparison.Ordinal);
                var lower = t.ToLowerInvariant();
                try
                {
                    if (lower.StartsWith("0x", StringComparison.Ordinal)) return Convert.ToInt64(lower.TrimEnd('l')[2..], 16).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (lower.StartsWith("0b", StringComparison.Ordinal)) return Convert.ToInt64(lower.TrimEnd('l')[2..], 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (lower.EndsWith('f')) return JavaDouble(double.Parse(lower.TrimEnd('f'), System.Globalization.CultureInfo.InvariantCulture)) + "f";
                    if (lower.EndsWith('d') || lower.Contains('.') || lower.Contains('e')) return JavaDouble(double.Parse(lower.TrimEnd('d'), System.Globalization.CultureInfo.InvariantCulture));
                    return lower.TrimEnd('l');
                }
                catch (Exception e) when (e is FormatException or OverflowException or ArgumentException)
                {
                    return t;
                }
            }
            default: return l.Text;
        }
    }

    private static string JavaDouble(double d)
    {
        if (d == Math.Floor(d) && Math.Abs(d) < 1e7) return d.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        return d.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }

    private string FormatClass(TypeInfo ti, TypeDecl d)
    {
        var sb = new StringBuilder();
        var ann = FormatAnnotations(d.Mods);
        if (ann.Length > 0) sb.Append(ann).Append('\n');
        Kw(sb, FormatAccess(ti.Flags));
        var isEnum = d.Kind == TypeKind.Enum;
        var isIface = d.Kind is TypeKind.Interface or TypeKind.Annotation;
        if (!isEnum && !isIface) Kw(sb, FormatModifiers(d.Mods.Flags));
        Kw(sb, d.Kind switch { TypeKind.Enum => "enum", TypeKind.Interface => "interface", TypeKind.Annotation => "@interface", TypeKind.Record => "record", _ => "class" });
        sb.Append(d.Name);
        if (d.TypeParams.Count > 0) sb.Append('<').Append(string.Join(", ", d.TypeParams.Select(tp => FormatTypeParam(tp, ws.TypeScopes[ti])))).Append('>');
        var parents = new List<JType>();
        var firstIsObject = true;
        var first = true;
        foreach (var p in ws.SuperTypes(ti))
        {
            if (first) { firstIsObject = p is ClassT { Name: "java/lang/Object" }; first = false; }
            if (p is ClassT { Name: "java/lang/Object" or "java/lang/Enum" or "java/lang/annotation/Annotation" or "java/lang/Record" }) continue;
            parents.Add(p);
        }
        if (parents.Count == 0) return sb.ToString();
        if (d.Kind is TypeKind.Class or TypeKind.Enum or TypeKind.Record)
        {
            if (isEnum || firstIsObject || d.Kind == TypeKind.Record)
            {
                sb.Append(" implements ").Append(string.Join(", ", parents.Select(FormatType)));
            }
            else
            {
                sb.Append(" extends ").Append(FormatType(parents[0]));
                if (parents.Count > 1) sb.Append(" implements ").Append(string.Join(", ", parents.Skip(1).Select(FormatType)));
            }
        }
        else sb.Append(" extends ").Append(string.Join(", ", parents.Select(FormatType)));
        return sb.ToString();
    }

    private string FormatTypeParam(TypeParam tp, Scope scope)
    {
        if (tp.Bounds.Count == 0) return tp.Name;
        var bounds = tp.Bounds.Select(b => FormatType(ws.ResolveType(b, scope, null))).ToList();
        if (bounds is ["Object"]) return tp.Name;
        return tp.Name + " extends " + string.Join(" & ", bounds);
    }

    private string FormatMethod(MethodInfo m, MethodDecl? d, Scope scope)
    {
        var sb = new StringBuilder();
        var ann = FormatAnnotations(d?.Mods);
        if (ann.Length > 0) sb.Append(ann).Append('\n');
        Kw(sb, FormatAccess(m.Flags));
        Kw(sb, FormatModifiers(m.Flags));
        if (d is { TypeParams.Count: > 0 })
        {
            Kw(sb, "<" + string.Join(", ", d.TypeParams.Select(tp => FormatTypeParam(tp, scope))) + ">");
        }
        if (m.IsCtor) sb.Append(m.Owner.Simple);
        else
        {
            Kw(sb, FormatType(m.Return));
            sb.Append(m.Name);
        }
        sb.Append('(');
        for (var i = 0; i < m.Params.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(FormatType(m.Params[i])).Append(' ').Append(i < m.ParamNames.Count ? m.ParamNames[i] : "arg" + i);
        }
        sb.Append(')');
        if (m.Throws.Count > 0) sb.Append(" throws ").Append(string.Join(", ", m.Throws.Select(FormatType)));
        return sb.ToString();
    }

    private string FormatField(FieldInfo f, Modifiers? mods, Scope scope)
    {
        var sb = new StringBuilder();
        var ann = FormatAnnotations(mods);
        if (ann.Length > 0) sb.Append(ann).Append('\n');
        Kw(sb, FormatAccess(f.Flags));
        Kw(sb, FormatModifiers(f.Flags));
        Kw(sb, FormatType(f.Type));
        sb.Append(f.Name);
        return sb.ToString();
    }

    private string FormatVar(LocalVar v)
    {
        var sb = new StringBuilder();
        var ann = FormatAnnotations(v.Mods);
        if (ann.Length > 0) sb.Append(ann).Append('\n');
        Kw(sb, FormatAccess(v.Flags));
        Kw(sb, FormatModifiers(v.Flags));
        Kw(sb, FormatType(v.Type));
        sb.Append(v.Name);
        return sb.ToString();
    }
}
