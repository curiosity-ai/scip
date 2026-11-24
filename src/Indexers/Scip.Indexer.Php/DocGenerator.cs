using System.Text;
using System.Text.RegularExpressions;
using Scip.Indexer.Php.Syntax;

namespace Scip.Indexer.Php;

/// <summary>
/// Port of <c>ScipPhp\DocGenerator</c> including the subset of php-parser's <c>PrettyPrinter\Standard</c> it relies on.
/// Expressions the printer does not know are printed as their original source text.
/// </summary>
internal sealed class DocGenerator
{
    private readonly string _src;

    public DocGenerator(string source) => _src = source;

    /// <summary>Returns the markdown documentation entries for a definition node.</summary>
    public List<string> Create(Node n)
    {
        var (sign, doc) = Signature(n);
        var s = $"```php\n{sign}\n```";
        return doc == "" ? [s] : [s, doc];
    }

    private (string Sign, string Doc) Signature(Node n)
    {
        switch (n)
        {
            case ConstItem c:
                return ConstSign(c);
            case ClassStmt cs:
                return ClassSign(cs);
            case InterfaceStmt i:
            {
                var sign = $"interface {i.Name!.Name}";
                if (i.Extends.Count > 0)
                {
                    sign += " implements " + PrintList(i.Extends);
                }

                return (sign, DocComment(i));
            }

            case TraitStmt t:
                return ($"trait {t.Name!.Name}", DocComment(t));
            case EnumStmt e:
                return ($"enum {e.Name!.Name}", DocComment(e));
            case EnumCase ec:
                return ($"case {ec.Name.Name}{(ec.Value != null ? " = " + PrintExpr(ec.Value) : "")};", DocComment(ec));
            case FunctionStmt f:
                return FuncSign(f);
            case ClassMethod m:
                return MethodSign(m);
            case Param p:
                return (PrintParam(p), DocComment(p));
            case PropertyItem pi:
                return PropertySign(pi);
        }

        throw new InvalidOperationException("Unexpected node type: " + n.GetType().Name);
    }

    private (string, string) ClassSign(ClassStmt c)
    {
        var sign = $"class {c.Name!.Name}";
        if (c.IsReadonly)
        {
            sign = "readonly " + sign;
        }

        if (c.IsAbstract)
        {
            sign = "abstract " + sign;
        }
        else if (c.IsFinal)
        {
            sign = "final " + sign;
        }

        if (c.Extends != null)
        {
            sign += " extends " + c.Extends.Text;
        }

        if (c.Implements.Count > 0)
        {
            sign += " implements " + PrintList(c.Implements);
        }

        return (sign, DocComment(c));
    }

    private (string, string) ConstSign(ConstItem c)
    {
        var sign = $"{c.Name.Name} = {PrintExpr(c.Value)}";
        if (c.Parent is not ClassConst cc)
        {
            return (sign, "");
        }

        if ((cc.Flags & Mod.Final) != 0)
        {
            sign = "final " + sign;
        }

        sign = Visibility(cc.Flags) + " " + sign;
        return (sign, DocComment(cc));
    }

    private (string, string) PropertySign(PropertyItem p)
    {
        if (p.Parent is not PropertyStmt ps)
        {
            return ($"${p.Name.Name}", "");
        }

        var modifiers = Visibility(ps.Flags);
        if ((ps.Flags & Mod.Static) != 0)
        {
            modifiers += " static";
        }

        if ((ps.Flags & Mod.ReadOnly) != 0)
        {
            modifiers += " readonly";
        }

        var sign = ps.Type != null ? $"{modifiers} {PrintType(ps.Type)} ${p.Name.Name}" : $"{modifiers} ${p.Name.Name}";
        if (p.Default != null)
        {
            sign += " = " + PrintExpr(p.Default);
        }

        return (sign, DocComment(ps));
    }

    private (string, string) FuncSign(FunctionStmt f)
    {
        var sign = $"function {f.Name.Name}({string.Join(", ", f.Params.Select(PrintParam))})";
        if (f.ReturnType != null)
        {
            sign += ": " + PrintType(f.ReturnType);
        }

        return (sign, DocComment(f));
    }

    private (string, string) MethodSign(ClassMethod m)
    {
        var modifiers = Visibility(m.Flags);
        if ((m.Flags & Mod.Static) != 0)
        {
            modifiers += " static";
        }

        if ((m.Flags & Mod.Final) != 0)
        {
            modifiers += " final";
        }
        else if ((m.Flags & Mod.Abstract) != 0)
        {
            modifiers += " abstract";
        }

        var sign = $"{modifiers} function {m.Name.Name}({string.Join(", ", m.Params.Select(PrintParam))})";
        if (m.ReturnType != null)
        {
            sign += ": " + PrintType(m.ReturnType);
        }

        return (sign, DocComment(m));
    }

    private static string Visibility(Mod flags) =>
        (flags & Mod.Private) != 0 ? "private" : (flags & Mod.Protected) != 0 ? "protected" : "public";

    private static string DocComment(Node n)
    {
        var comment = n.Doc?.Text ?? "";
        comment = Remove(@"^(\s+)?/\*\*\s", comment, 1);
        comment = Remove(@"^(\s+)?\*\s", comment, -1);
        comment = Remove(@"^(\s+)?\*/", comment, 1);
        comment = Remove(@"(\s+)?\*/$", comment, 1);
        return comment.Replace("\n", "<br>", StringComparison.Ordinal);
    }

    private static string Remove(string pattern, string subject, int count) =>
        new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant).Replace(subject, "", count);

    // ------------------------------------------------------------------ pretty printer

    private string PrintList(IEnumerable<Name> names) => string.Join("\n", names.Select(PrintName));

    private static string PrintName(Name n) => n.Kind == NameKind.FullyQualified ? "\\" + n.Text : n.Text;

    private string PrintType(Node t) => t switch
    {
        Name n => PrintName(n),
        Ident i => i.Name,
        NullableType nt => "?" + PrintType(nt.Type),
        UnionType u => string.Join("|", u.Types.Select(x => x is IntersectionType ? "(" + PrintType(x) + ")" : PrintType(x))),
        IntersectionType it => string.Join("&", it.Types.Select(PrintType)),
        _ => Raw(t),
    };

    private string PrintParam(Param p)
    {
        var sb = new StringBuilder();
        foreach (var g in p.AttrGroups)
        {
            sb.Append(Raw(g)).Append(' ');
        }

        sb.Append(Modifiers(p.Flags));
        if (p.Type != null)
        {
            sb.Append(PrintType(p.Type)).Append(' ');
        }

        if (p.ByRef)
        {
            sb.Append('&');
        }

        if (p.Variadic)
        {
            sb.Append("...");
        }

        sb.Append('$').Append(p.Var.Name);
        if (p.Default != null)
        {
            sb.Append(" = ").Append(PrintExpr(p.Default));
        }

        return sb.ToString();
    }

    private static string Modifiers(Mod f)
    {
        var sb = new StringBuilder();
        if ((f & Mod.Abstract) != 0)
        {
            sb.Append("abstract ");
        }

        if ((f & Mod.Final) != 0)
        {
            sb.Append("final ");
        }

        if ((f & Mod.Public) != 0)
        {
            sb.Append("public ");
        }

        if ((f & Mod.Protected) != 0)
        {
            sb.Append("protected ");
        }

        if ((f & Mod.Private) != 0)
        {
            sb.Append("private ");
        }

        if ((f & Mod.Static) != 0)
        {
            sb.Append("static ");
        }

        if ((f & Mod.ReadOnly) != 0)
        {
            sb.Append("readonly ");
        }

        return sb.ToString();
    }

    private string Raw(Node n) => n.End > n.Start && n.End <= _src.Length ? Lexer.Decode(_src[n.Start..n.End]) : "";

    private static int Prec(string op) => op switch
    {
        "or" => 1, "xor" => 2, "and" => 3, "??" => 6, "||" => 7, "&&" => 8, "|" => 9, "^" => 10, "&" => 11,
        "==" or "!=" or "===" or "!==" or "<>" or "<=>" => 12, "<" or "<=" or ">" or ">=" => 13, "." => 14, "<<" or ">>" => 15,
        "+" or "-" => 16, "*" or "/" or "%" => 17, "**" => 21, _ => 0,
    };

    private string PrintExpr(Node e) => e switch
    {
        NumberLit n => n.Text.Replace("_", "", StringComparison.Ordinal),
        StringLit s => s.Raw,
        TemplateExpr t => t.Raw,
        ConstFetch { Name: var cn } => cn.Text.ToLowerInvariant() is "true" or "false" or "null" ? cn.Text.ToLowerInvariant() : PrintName(cn),
        MagicConst m => m.Text,
        Variable { Name: { } vn } => "$" + vn,
        ClassConstFetch cf => PrintClassRef(cf.Class) + "::" + (cf.NameNode is Ident ci ? ci.Name : Raw(cf.NameNode)),
        StaticPropertyFetch sp => PrintClassRef(sp.Class) + "::$" + (sp.NameNode is Ident si ? si.Name : Raw(sp.NameNode)),
        PropertyFetch pf => PrintExpr(pf.Var) + (pf.Nullsafe ? "?->" : "->") + (pf.NameNode is Ident pi ? pi.Name : Raw(pf.NameNode)),
        ArrayDimFetch ad => PrintExpr(ad.Var) + "[" + (ad.Dim != null ? PrintExpr(ad.Dim) : "") + "]",
        ArrayLit a => PrintArray(a),
        BinaryOp b => PrintBinary(b),
        Ternary t => PrintExpr(t.Cond) + (t.If != null ? " ? " + PrintExpr(t.If) + " : " : " ?: ") + PrintExpr(t.Else),
        NewExpr { Class: Name nn } ne => "new " + PrintName(nn) + "(" + string.Join(", ", ne.Args.Select(PrintArg)) + ")",
        FuncCall { Callee: Name fn } fc => PrintName(fn) + "(" + string.Join(", ", fc.Args.Select(PrintArg)) + ")",
        StaticCall { NameNode: Ident mi } sc => PrintClassRef(sc.Class) + "::" + mi.Name + "(" + string.Join(", ", sc.Args.Select(PrintArg)) + ")",
        GenericExpr { Kind: "-" or "+" or "~" or "!" or "@", Kids: [{ } k] } g => g.Kind + PrintExpr(k),
        _ => Raw(e),
    };

    private string PrintArg(Arg a) => (a.Unpack ? "..." : "") + (a.Label != null ? a.Label.Name + ": " : "") + PrintExpr(a.Value);

    private string PrintClassRef(Node c) => c is Name n ? PrintName(n) : PrintExpr(c);

    private string PrintBinary(BinaryOp b)
    {
        var p = Prec(b.Op);
        string Side(Node n, bool right)
        {
            var s = PrintExpr(n);
            var np = n is BinaryOp cb ? Prec(cb.Op) : n is Ternary ? 5 : 100;
            return np < p || (np == p && right) ? "(" + s + ")" : s;
        }

        return Side(b.Left, false) + " " + b.Op + " " + Side(b.Right, true);
    }

    private string PrintArray(ArrayLit a)
    {
        var items = a.Items.Select(i => i == null ? "" : (i.Unpack ? "..." : "") + (i.Key != null ? PrintExpr(i.Key) + " => " : "") + (i.ByRef ? "&" : "") + (i.Value != null ? PrintExpr(i.Value) : ""));
        return a.IsList ? "array(" + string.Join(", ", items) + ")" : "[" + string.Join(", ", items) + "]";
    }
}
