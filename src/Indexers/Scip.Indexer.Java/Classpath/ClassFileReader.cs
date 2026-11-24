using System.Buffers.Binary;
using System.Text;
using Scip.Indexer.Java.Semantics;
using Scip.Indexer.Java.Syntax;

namespace Scip.Indexer.Java.Classpath;

/// <summary>Native JVM class file parser producing <see cref="TypeInfo"/> (members, supertypes, generic signatures).</summary>
public static class ClassFileReader
{
    private sealed class Pool
    {
        public object?[] Items = [];
        public byte[] Tags = [];

        public string Utf8(int i) => (string)Items[i]!;

        public string ClassName(int i) => i == 0 ? "" : Utf8((int)Items[i]!);
    }

    private sealed class Reader(byte[] b)
    {
        public int P;

        public int U1() => b[P++];

        public int U2()
        {
            var v = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(P));
            P += 2;
            return v;
        }

        public int U4()
        {
            var v = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(P));
            P += 4;
            return v;
        }
    }

    /// <summary>Parses <paramref name="bytes"/>; returns null when the data is not a valid class file.</summary>
    public static TypeInfo? Read(byte[] bytes, JavaPackage origin)
    {
        try
        {
            return ReadCore(bytes, origin);
        }
        catch (Exception e) when (e is IndexOutOfRangeException or ArgumentOutOfRangeException or InvalidCastException or InvalidDataException)
        {
            return null;
        }
    }

    private static TypeInfo? ReadCore(byte[] bytes, JavaPackage origin)
    {
        var r = new Reader(bytes);
        if (r.U4() != unchecked((int)0xCAFEBABE)) return null;
        r.U2();
        r.U2();
        var n = r.U2();
        var pool = new Pool { Items = new object?[n], Tags = new byte[n] };
        for (var i = 1; i < n; i++)
        {
            var tag = (byte)r.U1();
            pool.Tags[i] = tag;
            switch (tag)
            {
                case 1:
                {
                    var len = r.U2();
                    pool.Items[i] = Encoding.UTF8.GetString(bytes, r.P, len);
                    r.P += len;
                    break;
                }
                case 3 or 4: r.P += 4; break;
                case 5 or 6: r.P += 8; i++; break;
                case 7 or 8 or 16 or 19 or 20: pool.Items[i] = r.U2(); break;
                case 9 or 10 or 11 or 12 or 17 or 18: r.P += 4; break;
                case 15: r.P += 3; break;
                default: throw new InvalidDataException("bad constant pool tag " + tag);
            }
        }
        var access = r.U2();
        var thisName = pool.ClassName(r.U2());
        var superName = pool.ClassName(r.U2());
        var ifaceCount = r.U2();
        var ifaces = new List<string>();
        for (var i = 0; i < ifaceCount; i++) ifaces.Add(pool.ClassName(r.U2()));

        var ti = new TypeInfo
        {
            Name = thisName,
            Simple = thisName[(thisName.LastIndexOfAny(['/', '$']) + 1)..],
            Flags = ToMods(access, false),
            Origin = origin,
            Kind = (access & 0x2000) != 0 ? TypeKind.Annotation : (access & 0x200) != 0 ? TypeKind.Interface : (access & 0x4000) != 0 ? TypeKind.Enum : TypeKind.Class,
        };
        ti.Super = superName.Length > 0 ? new ClassT(superName, []) : null;
        ti.Interfaces = ifaces.Select(i => (JType)new ClassT(i, [])).ToList();
        if (superName == "java/lang/Record") ti.Kind = TypeKind.Record;

        var fieldCount = r.U2();
        var fields = new List<(int Access, string Name, string Desc, string? Sig)>();
        for (var i = 0; i < fieldCount; i++)
        {
            var a = r.U2();
            var name = pool.Utf8(r.U2());
            var desc = pool.Utf8(r.U2());
            string? sig = null;
            var ac = r.U2();
            for (var k = 0; k < ac; k++)
            {
                var an = pool.Utf8(r.U2());
                var len = r.U4();
                if (an == "Signature") sig = pool.Utf8(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(r.P)));
                r.P += len;
            }
            if ((a & 0x1000) != 0) continue;
            fields.Add((a, name, desc, sig));
        }
        var methodCount = r.U2();
        var methods = new List<(int Access, string Name, string Desc, string? Sig)>();
        for (var i = 0; i < methodCount; i++)
        {
            var a = r.U2();
            var name = pool.Utf8(r.U2());
            var desc = pool.Utf8(r.U2());
            string? sig = null;
            var ac = r.U2();
            for (var k = 0; k < ac; k++)
            {
                var an = pool.Utf8(r.U2());
                var len = r.U4();
                if (an == "Signature") sig = pool.Utf8(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(r.P)));
                r.P += len;
            }
            if ((a & 0x1040) != 0 || name == "<clinit>") continue; // synthetic or bridge
            methods.Add((a, name, desc, sig));
        }
        string? classSig = null;
        var attrCount = r.U2();
        var inner = new List<(string Inner, string? Outer, string? Name, int Flags)>();
        for (var k = 0; k < attrCount; k++)
        {
            var an = pool.Utf8(r.U2());
            var len = r.U4();
            var end = r.P + len;
            if (an == "Signature") classSig = pool.Utf8(r.U2());
            else if (an == "InnerClasses")
            {
                var cnt = r.U2();
                for (var i = 0; i < cnt; i++)
                {
                    var ic = r.U2();
                    var oc = r.U2();
                    var nm = r.U2();
                    var fl = r.U2();
                    inner.Add((pool.ClassName(ic), oc == 0 ? null : pool.ClassName(oc), nm == 0 ? null : pool.Utf8(nm), fl));
                }
            }
            r.P = end;
        }

        foreach (var (innerName, outerName, simple, flags) in inner)
        {
            if (innerName == thisName && outerName is not null && simple is not null)
            {
                ti.Flags = ToMods(flags, false);
                ti.OuterName = outerName;
                ti.Simple = simple;
            }
            else if (outerName == thisName && simple is not null) ti.MemberTypes[simple] = innerName;
        }

        if (classSig is not null) ApplyClassSignature(ti, classSig);

        foreach (var (a, name, desc, sig) in fields)
        {
            var f = new FieldInfo { Name = name, Flags = ToMods(a, false), Owner = ti, IsEnumConstant = (a & 0x4000) != 0 };
            f.Type = SignatureParser.ParseType(sig ?? desc);
            ti.Fields.Add(f);
        }
        foreach (var (a, name, desc, sig) in methods)
        {
            var m = new MethodInfo { Name = name, Flags = ToMods(a, true), Owner = ti, Varargs = (a & 0x80) != 0 };
            var parsed = SignatureParser.ParseMethod(sig ?? desc);
            m.Params = parsed.Params;
            if (sig is not null && parsed.Params.Count != SignatureParser.ParseMethod(desc).Params.Count)
            {
                // inner-class / enum constructors: generic signature omits synthetic parameters; keep the descriptor view
                m.Params = SignatureParser.ParseMethod(desc).Params;
            }
            m.Return = parsed.Return;
            m.TypeParams = parsed.TypeParams;
            m.Throws = parsed.Throws;
            if (ti.IsInterface && (a & 0x400) == 0 && (a & 0x8) == 0) m.Flags |= Mod.Default;
            ti.Methods.Add(m);
        }
        return ti;
    }

    private static void ApplyClassSignature(TypeInfo ti, string sig)
    {
        var p = new SignatureParser(sig);
        var tps = p.TypeParams();
        ti.TypeParams = tps.Select(t => t.Name).ToList();
        ti.TypeParamBounds = tps.Select(t => t.Bound).ToList();
        ti.Super = p.Type();
        var ifs = new List<JType>();
        while (!p.End) ifs.Add(p.Type());
        ti.Interfaces = ifs;
    }

    internal static Mod ToMods(int access, bool method)
    {
        var m = Mod.None;
        if ((access & 0x1) != 0) m |= Mod.Public;
        if ((access & 0x2) != 0) m |= Mod.Private;
        if ((access & 0x4) != 0) m |= Mod.Protected;
        if ((access & 0x8) != 0) m |= Mod.Static;
        if ((access & 0x10) != 0) m |= Mod.Final;
        if ((access & 0x400) != 0) m |= Mod.Abstract;
        return m;
    }
}

/// <summary>Parser for JVMS 4.7.9.1 signatures and field/method descriptors.</summary>
public sealed class SignatureParser(string s)
{
    private int _i;

    public bool End => _i >= s.Length;

    public sealed record MethodSig(List<string> TypeParams, List<JType> Params, JType Return, List<JType> Throws);

    public static JType ParseType(string sig) => new SignatureParser(sig).Type();

    public static MethodSig ParseMethod(string sig)
    {
        var p = new SignatureParser(sig);
        var tps = p.TypeParams();
        p._i++; // (
        var ps = new List<JType>();
        while (p._i < sig.Length && sig[p._i] != ')') ps.Add(p.Type());
        p._i++;
        var ret = p.Type();
        var thr = new List<JType>();
        while (p._i < sig.Length && sig[p._i] == '^') { p._i++; thr.Add(p.Type()); }
        return new MethodSig(tps.Select(t => t.Name).ToList(), ps, ret, thr);
    }

    public List<(string Name, JType Bound)> TypeParams()
    {
        var list = new List<(string, JType)>();
        if (_i >= s.Length || s[_i] != '<') return list;
        _i++;
        while (s[_i] != '>')
        {
            var c = s.IndexOf(':', _i);
            var name = s[_i..c];
            _i = c;
            JType bound = JType.Object;
            var first = true;
            while (_i < s.Length && s[_i] == ':')
            {
                _i++;
                if (s[_i] == ':') continue;
                var b = Type();
                if (first) bound = b;
                first = false;
            }
            list.Add((name, bound));
        }
        _i++;
        return list;
    }

    public JType Type()
    {
        var c = s[_i++];
        switch (c)
        {
            case 'B': return new PrimT("byte");
            case 'C': return new PrimT("char");
            case 'D': return new PrimT("double");
            case 'F': return new PrimT("float");
            case 'I': return new PrimT("int");
            case 'J': return new PrimT("long");
            case 'S': return new PrimT("short");
            case 'Z': return new PrimT("boolean");
            case 'V': return JType.Void;
            case '[': return new ArrayT(Type());
            case 'T':
            {
                var e = s.IndexOf(';', _i);
                var name = s[_i..e];
                _i = e + 1;
                return new VarT(name);
            }
            case 'L': return ClassType();
            default: throw new InvalidDataException("bad signature " + s);
        }
    }

    private JType ClassType()
    {
        var name = new System.Text.StringBuilder();
        IReadOnlyList<JType> args = [];
        while (true)
        {
            var c = s[_i];
            if (c == ';') { _i++; break; }
            if (c == '<')
            {
                _i++;
                var list = new List<JType>();
                while (s[_i] != '>')
                {
                    var a = s[_i];
                    if (a == '*') { _i++; list.Add(new WildT(WildKind.Unbounded, null)); }
                    else if (a == '+') { _i++; list.Add(new WildT(WildKind.Extends, Type())); }
                    else if (a == '-') { _i++; list.Add(new WildT(WildKind.Super, Type())); }
                    else list.Add(Type());
                }
                _i++;
                args = list;
                continue;
            }
            if (c == '.') { name.Append('$'); args = []; _i++; continue; }
            name.Append(c);
            _i++;
        }
        return new ClassT(name.ToString(), args);
    }
}
