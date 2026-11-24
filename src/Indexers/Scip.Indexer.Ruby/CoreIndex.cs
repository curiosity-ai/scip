namespace Scip.Indexer.Ruby;

/// <summary>A class or module of the embedded Ruby core/stdlib payload (see <c>Payload/generate_core.py</c>).</summary>
internal sealed class CoreType(string name, bool isModule, string? super)
{
    public string Name { get; } = name;

    public bool IsModule { get; } = isModule;

    public string? Super { get; } = super;

    public List<string> Includes { get; } = [];

    public List<string> Extends { get; } = [];

    /// <summary>Instance methods to their simple return type: a class name, <c>@</c> (self type), <c>^</c> (attached class) or null.</summary>
    public Dictionary<string, string?> Instance { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, string?> Singleton { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Constants { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Minimal replacement for Sorbet's serialized core/stdlib RBI payload: names, ancestry, method names and simple
/// return types, which is all the native indexer needs to give core references a symbol.
/// </summary>
internal static class CoreIndex
{
    private static readonly Lazy<Dictionary<string, CoreType>> Types = new(Load);

    public static CoreType? Find(string key) => Types.Value.GetValueOrDefault(key);

    private static Dictionary<string, CoreType> Load()
    {
        var types = new Dictionary<string, CoreType>(StringComparer.Ordinal);
        CoreType? cur = null;
        foreach (var raw in CorePayloadData.Text.Split('\n'))
        {
            if (raw.Length < 3)
            {
                continue;
            }

            var tag = raw[0];
            var parts = raw.Split(' ', 3);
            switch (tag)
            {
                case 'C' or 'M':
                    if (!types.TryGetValue(parts[1], out cur))
                    {
                        types[parts[1]] = cur = new CoreType(parts[1], tag == 'M', parts.Length > 2 ? parts[2] : (tag == 'C' && parts[1] != "BasicObject" ? "Object" : null));
                    }

                    break;
                case '@':
                    cur = types.GetValueOrDefault(parts[1]);
                    break;
                case 'I':
                    cur?.Includes.Add(parts[1]);
                    break;
                case 'E':
                    cur?.Extends.Add(parts[1]);
                    break;
                case 'i':
                    cur?.Instance.TryAdd(parts[1], parts.Length > 2 ? parts[2] : null);
                    break;
                case 's':
                    cur?.Singleton.TryAdd(parts[1], parts.Length > 2 ? parts[2] : null);
                    break;
                case 'k':
                    cur?.Constants.Add(parts[1]);
                    break;
            }
        }

        return types;
    }
}
