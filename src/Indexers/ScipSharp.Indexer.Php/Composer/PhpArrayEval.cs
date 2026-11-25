using ScipSharp.Indexer.Php.Syntax;

namespace ScipSharp.Indexer.Php.Composer;

/// <summary>A PHP array value: ordered list of key / value pairs.</summary>
internal sealed class PhpArr
{
    public List<KeyValuePair<string, object?>> Items { get; } = [];

    public object? Get(string key)
    {
        foreach (var kv in Items)
        {
            if (kv.Key == key)
            {
                return kv.Value;
            }
        }

        return null;
    }
}

/// <summary>
/// Evaluates the tiny subset of PHP found in composer's generated metadata files (<c>installed.php</c>,
/// <c>autoload_*.php</c>): literals, arrays, <c>__DIR__</c>, <c>dirname()</c>, concatenation and top-level variables.
/// This is how the native indexer replaces upstream's <c>require</c> of those files.
/// </summary>
internal static class PhpArrayEval
{
    public static PhpArr? EvaluateReturnedArray(string path, PhpFs fs)
    {
        var file = fs.Parse(path);
        if (file == null)
        {
            return null;
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? "/";
        var vars = new Dictionary<string, object?>();
        foreach (var s in file.Stmts)
        {
            switch (s)
            {
                case ExprStmt { Expr: Assign { Var: Variable { Name: { } name }, Value: var v } }:
                    vars[name] = Eval(v, dir, vars);
                    break;
                case GenericStmt { Kind: "return", Kids: [Expr e] }:
                    return Eval(e, dir, vars) as PhpArr;
            }
        }

        return null;
    }

    private static object? Eval(Node n, string dir, Dictionary<string, object?> vars)
    {
        switch (n)
        {
            case StringLit s:
                return s.Value;
            case NumberLit num:
                return long.TryParse(num.Text, out var l) ? l : null;
            case ConstFetch { Name.Text: var c }:
                return c.ToLowerInvariant() switch { "true" => true, "false" => false, _ => null };
            case MagicConst { Text: var m } when m.Equals("__DIR__", StringComparison.OrdinalIgnoreCase):
                return dir;
            case Variable { Name: { } v }:
                return vars.GetValueOrDefault(v);
            case BinaryOp { Op: ".", Left: var l1, Right: var r1 }:
                return Convert.ToString(Eval(l1, dir, vars)) + Convert.ToString(Eval(r1, dir, vars));
            case FuncCall { Callee: Name { Text: var fn }, Args: [var a0, ..] } when fn.Equals("dirname", StringComparison.OrdinalIgnoreCase):
            {
                var p = Convert.ToString(Eval(a0.Value, dir, vars)) ?? "";
                return Path.GetDirectoryName(p.TrimEnd('/')) ?? "/";
            }

            case ArrayLit arr:
            {
                var result = new PhpArr();
                long next = 0;
                foreach (var item in arr.Items)
                {
                    if (item?.Value == null)
                    {
                        continue;
                    }

                    var value = Eval(item.Value, dir, vars);
                    string key;
                    if (item.Key != null)
                    {
                        key = Convert.ToString(Eval(item.Key, dir, vars)) ?? "";
                        if (long.TryParse(key, out var kn) && kn >= next)
                        {
                            next = kn + 1;
                        }
                    }
                    else
                    {
                        key = (next++).ToString();
                    }

                    result.Items.Add(new KeyValuePair<string, object?>(key, value));
                }

                return result;
            }
        }

        return null;
    }
}
