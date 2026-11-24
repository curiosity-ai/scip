using System.Buffers.Binary;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Text;
using Scip.Indexer.Java.Semantics;
using Scip.Indexer.Java.Syntax;

namespace Scip.Indexer.Java.Classpath;

/// <summary>
/// Native reader for the JDK's <c>lib/modules</c> jimage container (JDK 9+), so JDK classes can be read without a JVM.
/// Only uncompressed resources are supported (the default for JDK builds).
/// </summary>
public sealed class JImage : IDisposable
{
    private readonly MemoryMappedFile _mmf;
    private readonly MemoryMappedViewAccessor _view;
    private readonly byte[] _index;
    private readonly int _tableLength;
    private readonly int _redirectOff;
    private readonly int _offsetsOff;
    private readonly int _locationsOff;
    private readonly int _stringsOff;
    private readonly long _indexSize;
    private readonly Dictionary<string, (int Module, long Offset, long Size, long Compressed)> _classes = new(StringComparer.Ordinal);
    private readonly List<string> _modules = [];

    public JImage(string path)
    {
        _mmf = MemoryMappedFile.CreateFromFile(path, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
        _view = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        var header = new byte[28];
        _view.ReadArray(0, header, 0, 28);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != 0xCAFEDADA) throw new InvalidDataException("unsupported jimage: " + path);
        var tableLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(16));
        var locationsSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(20));
        var stringsSize = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(24));
        _tableLength = tableLength;
        _redirectOff = 28;
        _offsetsOff = _redirectOff + tableLength * 4;
        _locationsOff = _offsetsOff + tableLength * 4;
        _stringsOff = _locationsOff + locationsSize;
        _indexSize = _stringsOff + stringsSize;
        _index = new byte[_indexSize];
        _view.ReadArray(0, _index, 0, (int)_indexSize);
        Scan();
    }

    private string Str(int off)
    {
        var start = _stringsOff + off;
        var end = start;
        while (_index[end] != 0) end++;
        return Encoding.UTF8.GetString(_index, start, end - start);
    }

    private void Scan()
    {
        for (var i = 0; i < _tableLength; i++)
        {
            var off = BinaryPrimitives.ReadInt32LittleEndian(_index.AsSpan(_offsetsOff + i * 4));
            var attrs = new long[8];
            var p = _locationsOff + off;
            while (true)
            {
                var b = _index[p];
                var kind = b >> 3;
                if (kind == 0) break;
                var len = (b & 7) + 1;
                long v = 0;
                for (var k = 0; k < len; k++) v = (v << 8) | _index[p + 1 + k];
                if (kind < 8) attrs[kind] = v;
                p += 1 + len;
            }
            // 1 module, 2 parent, 3 base, 4 extension, 5 offset, 6 compressed, 7 uncompressed
            if (attrs[4] == 0 || Str((int)attrs[4]) != "class" || attrs[1] == 0) continue;
            var parent = Str((int)attrs[2]);
            var name = (parent.Length > 0 ? parent + "/" : "") + Str((int)attrs[3]);
            var module = Str((int)attrs[1]);
            var mi = _modules.IndexOf(module);
            if (mi < 0) { _modules.Add(module); mi = _modules.Count - 1; }
            _classes.TryAdd(name, (mi, attrs[5], attrs[7], attrs[6]));
        }
    }

    public bool Contains(string internalName) => _classes.ContainsKey(internalName);

    public IEnumerable<string> ClassNames => _classes.Keys;

    public byte[]? ReadClass(string internalName)
    {
        if (!_classes.TryGetValue(internalName, out var loc) || loc.Compressed != 0) return null;
        var buf = new byte[loc.Size];
        _view.ReadArray(_indexSize + loc.Offset, buf, 0, buf.Length);
        return buf;
    }

    public void Dispose()
    {
        _view.Dispose();
        _mmf.Dispose();
    }
}

/// <summary>
/// Looks up <see cref="TypeInfo"/> by JVM internal name across source types, project output directories, jars and
/// the JDK runtime image. Replaces javac's symbol table for everything that is not defined in the indexed sources.
/// </summary>
public sealed class ClassPath : IDisposable
{
    private readonly Dictionary<string, TypeInfo?> _cache = new(StringComparer.Ordinal);
    private readonly List<(MavenPackage Pkg, Dictionary<string, ZipArchiveEntry> Entries, ZipArchive Zip)> _jars = [];
    private readonly List<(MavenPackage Pkg, string Dir)> _dirs = [];
    private readonly HashSet<string> _packages = new(StringComparer.Ordinal);
    private readonly HashSet<string> _sourcePackages = new(StringComparer.Ordinal);
    private JImage? _jdk;
    private ZipArchive? _jdkFallback;
    private Dictionary<string, ZipArchiveEntry>? _jdkFallbackEntries;
    private ZipArchive? _ctSym;
    private Dictionary<string, List<(string Releases, ZipArchiveEntry Entry)>>? _ctIndex;
    private readonly Dictionary<(int Release, string Name), TypeInfo?> _jdkCache = [];
    private readonly HashSet<string> _nonJdkMisses = new(StringComparer.Ordinal);

    /// <summary>
    /// The <c>--release</c> the file being indexed is compiled with. For a release older than the running JDK javac reads
    /// the API from <c>lib/ct.sym</c> (public members only, in a different member order), which determines overload
    /// numbering of JDK symbols; <see langword="null"/> means the full JDK image.
    /// </summary>
    public int? Release { get; set; }

    private int JdkMajor => int.TryParse(Jdk.JdkVersion, out var v) ? v : 0;

    public JdkPackage Jdk { get; private set; } = new("17");

    public string? JdkHome { get; private set; }

    public void AddSource(TypeInfo t)
    {
        _cache[t.Name] = t;
        _sourcePackages.Add(t.Package);
        AddPackageChain(t.Package);
    }

    public void AddJar(MavenPackage pkg)
    {
        if (Directory.Exists(pkg.Jar))
        {
            _dirs.Add((pkg, pkg.Jar));
            foreach (var f in Directory.EnumerateFiles(pkg.Jar, "*.class", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(pkg.Jar, f).Replace('\\', '/');
                var slash = rel.LastIndexOf('/');
                if (slash > 0) AddPackageChain(rel[..slash]);
            }
            return;
        }
        if (!File.Exists(pkg.Jar)) return;
        try
        {
            var zip = ZipFile.OpenRead(pkg.Jar);
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            foreach (var e in zip.Entries)
            {
                if (!e.FullName.EndsWith(".class", StringComparison.Ordinal)) continue;
                var name = e.FullName[..^6];
                if (name.StartsWith("META-INF/", StringComparison.Ordinal)) continue;
                entries.TryAdd(name, e);
                var slash = name.LastIndexOf('/');
                if (slash > 0) AddPackageChain(name[..slash]);
            }
            _jars.Add((pkg, entries, zip));
        }
        catch (InvalidDataException)
        {
            // corrupt jar: ignore
        }
    }

    private void AddPackageChain(string pkg)
    {
        while (pkg.Length > 0 && _packages.Add(pkg))
        {
            var slash = pkg.LastIndexOf('/');
            pkg = slash < 0 ? "" : pkg[..slash];
        }
    }

    public bool PackageExists(string pkg) => _packages.Contains(pkg) || _sourcePackages.Contains(pkg) || JdkPackageExists(pkg);

    private HashSet<string>? _jdkPackages;

    private bool JdkPackageExists(string pkg)
    {
        if (_jdkPackages is null)
        {
            _jdkPackages = new HashSet<string>(StringComparer.Ordinal);
            IEnumerable<string> names = _jdk?.ClassNames ?? (IEnumerable<string>?)_jdkFallbackEntries?.Keys ?? [];
            foreach (var n in names)
            {
                var slash = n.LastIndexOf('/');
                var p = slash < 0 ? "" : n[..slash];
                while (p.Length > 0 && _jdkPackages.Add(p))
                {
                    var s2 = p.LastIndexOf('/');
                    p = s2 < 0 ? "" : p[..s2];
                }
            }
        }
        return _jdkPackages.Contains(pkg);
    }

    /// <summary>Locates the JDK runtime image under <paramref name="home"/> (JDK 9+ <c>lib/modules</c> or JDK 8 <c>rt.jar</c>).</summary>
    public bool UseJdk(string? home, string? versionOverride = null)
    {
        home ??= Environment.GetEnvironmentVariable("JAVA_HOME");
        if (string.IsNullOrEmpty(home)) home = DiscoverJavaHome();
        if (home is null) return false;
        JdkHome = home;
        var version = versionOverride;
        var rel = Path.Combine(home, "release");
        if (version is null && File.Exists(rel))
        {
            foreach (var line in File.ReadLines(rel))
            {
                if (!line.StartsWith("JAVA_VERSION=", StringComparison.Ordinal)) continue;
                var v = line["JAVA_VERSION=".Length..].Trim('"');
                version = v.StartsWith("1.", StringComparison.Ordinal) ? v.Split('.')[1] : JdkPackage.Parse(v).JdkVersion;
            }
        }
        if (version is not null) Jdk = new JdkPackage(version);
        var modules = Path.Combine(home, "lib", "modules");
        try
        {
            if (File.Exists(modules)) { _jdk = new JImage(modules); return true; }
            foreach (var rt in new[] { Path.Combine(home, "jre", "lib", "rt.jar"), Path.Combine(home, "lib", "rt.jar") })
            {
                if (!File.Exists(rt)) continue;
                _jdkFallback = ZipFile.OpenRead(rt);
                _jdkFallbackEntries = _jdkFallback.Entries.Where(e => e.FullName.EndsWith(".class", StringComparison.Ordinal))
                    .ToDictionary(e => e.FullName[..^6], e => e, StringComparer.Ordinal);
                return true;
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
        return false;
    }

    /// <summary>Major version of the JDK at <paramref name="home"/> (or <c>JAVA_HOME</c> / the <c>java</c> on <c>PATH</c>), if it can be determined.</summary>
    public static int? DetectJdkMajor(string? home)
    {
        home ??= Environment.GetEnvironmentVariable("JAVA_HOME");
        if (string.IsNullOrEmpty(home)) home = DiscoverJavaHome();
        var rel = home is null ? null : Path.Combine(home, "release");
        if (rel is null || !File.Exists(rel)) return null;
        foreach (var line in File.ReadLines(rel))
        {
            if (!line.StartsWith("JAVA_VERSION=", StringComparison.Ordinal)) continue;
            var v = line["JAVA_VERSION=".Length..].Trim('"');
            var major = v.StartsWith("1.", StringComparison.Ordinal) ? v.Split('.')[1] : JdkPackage.Parse(v).JdkVersion;
            return int.TryParse(major, out var n) ? n : null;
        }
        return null;
    }

    private static string? DiscoverJavaHome()
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var java = Path.Combine(dir, "java");
            if (!File.Exists(java)) continue;
            var real = new FileInfo(java).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? java;
            var home = Path.GetDirectoryName(Path.GetDirectoryName(real));
            if (home is not null && (File.Exists(Path.Combine(home, "lib", "modules")) || File.Exists(Path.Combine(home, "jre", "lib", "rt.jar")))) return home;
        }
        return null;
    }

    public TypeInfo? Load(string internalName)
    {
        if (_cache.TryGetValue(internalName, out var hit)) return hit;
        var ti = _nonJdkMisses.Contains(internalName) ? null : LoadUncached(internalName);
        if (ti is null) _nonJdkMisses.Add(internalName);
        else
        {
            _cache[internalName] = ti;
            if (ti.OuterName is { } on) ti.Outer = Load(on);
            return ti;
        }
        // JDK classes depend on the compile release, so they are cached per release
        var release = EffectiveRelease();
        if (_jdkCache.TryGetValue((release, internalName), out var jdkHit)) return jdkHit;
        var jt = LoadJdk(internalName, release);
        _jdkCache[(release, internalName)] = jt;
        if (jt?.OuterName is { } jon) jt.Outer = Load(jon);
        return jt;
    }

    private int EffectiveRelease()
    {
        if (Release is not { } r || r >= JdkMajor || r < 8 || JdkHome is null) return 0;
        return OpenCtSym() ? r : 0;
    }

    private bool OpenCtSym()
    {
        if (_ctIndex is not null) return true;
        if (_ctSym is not null || JdkHome is null) return false;
        var path = Path.Combine(JdkHome, "lib", "ct.sym");
        if (!File.Exists(path)) return false;
        try
        {
            _ctSym = ZipFile.OpenRead(path);
            _ctIndex = new Dictionary<string, List<(string, ZipArchiveEntry)>>(StringComparer.Ordinal);
            foreach (var e in _ctSym.Entries)
            {
                if (!e.FullName.EndsWith(".sig", StringComparison.Ordinal)) continue;
                var first = e.FullName.IndexOf('/', StringComparison.Ordinal);
                var second = first < 0 ? -1 : e.FullName.IndexOf('/', first + 1);
                if (second < 0) continue;
                var name = e.FullName[(second + 1)..^4];
                if (!_ctIndex.TryGetValue(name, out var list)) _ctIndex[name] = list = [];
                list.Add((e.FullName[..first], e));
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _ctIndex = null;
            return false;
        }
    }

    private TypeInfo? LoadJdk(string name, int release)
    {
        if (release != 0)
        {
            var c = (char)(release <= 9 ? '0' + release : 'A' + release - 10);
            if (_ctIndex!.TryGetValue(name, out var list))
            {
                foreach (var (releases, entry) in list)
                {
                    if (!releases.Contains(c, StringComparison.Ordinal)) continue;
                    using var s = entry.Open();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    return ClassFileReader.Read(ms.ToArray(), Jdk);
                }
            }
            return null;
        }
        if (_jdk is not null && _jdk.ReadClass(name) is { } bytes) return ClassFileReader.Read(bytes, Jdk);
        if (_jdkFallbackEntries is not null && _jdkFallbackEntries.TryGetValue(name, out var je))
        {
            using var s = je.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ClassFileReader.Read(ms.ToArray(), Jdk);
        }
        return null;
    }

    private TypeInfo? LoadUncached(string name)
    {
        foreach (var (pkg, entries, _) in _jars)
        {
            if (!entries.TryGetValue(name, out var e)) continue;
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var t = ClassFileReader.Read(ms.ToArray(), pkg);
            if (t is not null) return t;
        }
        foreach (var (pkg, dir) in _dirs)
        {
            var f = Path.Combine(dir, name + ".class");
            if (File.Exists(f) && ClassFileReader.Read(File.ReadAllBytes(f), pkg) is { } t) return t;
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var (_, _, zip) in _jars) zip.Dispose();
        _jdk?.Dispose();
        _jdkFallback?.Dispose();
        _ctSym?.Dispose();
    }
}
