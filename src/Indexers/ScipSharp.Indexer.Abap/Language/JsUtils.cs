// Helpers reproducing JavaScript semantics used by the ported semantic layer (abap/types, abap/5_syntax,
// objects, registry). Not a port of an upstream file.
using System.Collections;
using System.Text.RegularExpressions;

namespace ScipSharp.Indexer.Abap;

public static class JsUtils
{
    /// <summary>String(x) / template literal interpolation: undefined becomes "undefined".</summary>
    public static string Str(object? o)
    {
        return o switch
        {
            null => "undefined",
            bool b => b ? "true" : "false",
            string s => s,
            _ => o.ToString() ?? "",
        };
    }

    /// <summary>toUpperCase() on a value that may be undefined after an optional chain (x?.y().toUpperCase()).</summary>
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(s))]
    public static string? ToUpper(string? s) => s == null ? null : JsString.ToUpper(s);

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(s))]
    public static string? Trim(string? s) => s == null ? null : JsString.Trim(s);

    /// <summary>String.prototype.replace with a regex; null stays null (x?.y().replace(...)).</summary>
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(input))]
    public static string? RegexReplace(string? input, string pattern, string replacement, RegexOptions options)
    {
        return input == null ? null : Regex.Replace(input, pattern, replacement, options);
    }

    public static string Repeat(string s, int count)
    {
        if (count <= 0)
        {
            return "";
        }
        return string.Concat(Enumerable.Repeat(s, count));
    }

    /// <summary>String.prototype.substring: clamps, swaps when start &gt; end.</summary>
    public static string Substring(string s, int start, int? end = null)
    {
        var e = end ?? s.Length;
        start = Math.Clamp(start, 0, s.Length);
        e = Math.Clamp(e, 0, s.Length);
        if (start > e)
        {
            (start, e) = (e, start);
        }
        return s.Substring(start, e - start);
    }

    /// <summary>String.prototype.substr(start, length).</summary>
    public static string Substr(string s, int start, int? length = null)
    {
        if (start < 0)
        {
            start = Math.Max(s.Length + start, 0);
        }
        start = Math.Min(start, s.Length);
        var len = length ?? s.Length - start;
        len = Math.Clamp(len, 0, s.Length - start);
        return s.Substring(start, len);
    }

    /// <summary>String.prototype.slice(start, end).</summary>
    public static string Slice(string s, int start, int? end = null)
    {
        var e = end ?? s.Length;
        if (start < 0) start = Math.Max(s.Length + start, 0);
        if (e < 0) e = Math.Max(s.Length + e, 0);
        start = Math.Min(start, s.Length);
        e = Math.Min(e, s.Length);
        if (start >= e)
        {
            return "";
        }
        return s.Substring(start, e - start);
    }

    public static List<T> Slice<T>(IReadOnlyList<T> list, int start, int? end = null)
    {
        var e = end ?? list.Count;
        if (start < 0) start = Math.Max(list.Count + start, 0);
        if (e < 0) e = Math.Max(list.Count + e, 0);
        start = Math.Min(start, list.Count);
        e = Math.Min(e, list.Count);
        var ret = new List<T>();
        for (var i = start; i < e; i++)
        {
            ret.Add(list[i]);
        }
        return ret;
    }

    public static string CharAt(string s, int index)
    {
        if (index < 0 || index >= s.Length)
        {
            return "";
        }
        return s[index].ToString();
    }

    /// <summary>String.prototype.replace with a string pattern: only the first occurrence.</summary>
    public static string ReplaceFirst(string s, string search, string replacement)
    {
        var i = s.IndexOf(search, StringComparison.Ordinal);
        if (i < 0)
        {
            return s;
        }
        return s.Substring(0, i) + replacement + s.Substring(i + search.Length);
    }

    /// <summary>String.prototype.match: null when no match; groups for a non global regex, all matches for a global one.</summary>
    public static List<string>? Match(string s, string pattern, RegexOptions options, bool global)
    {
        var re = new Regex(pattern, options);
        if (global)
        {
            var all = re.Matches(s).Select(m => m.Value).ToList();
            return all.Count == 0 ? null : all;
        }
        var match = re.Match(s);
        if (!match.Success)
        {
            return null;
        }
        return match.Groups.Cast<Group>().Select(g => g.Success ? g.Value : null!).ToList();
    }

    public static List<string>? Match(string s, Regex re)
    {
        var match = re.Match(s);
        if (!match.Success)
        {
            return null;
        }
        return match.Groups.Cast<Group>().Select(g => g.Success ? g.Value : null!).ToList();
    }

    public static List<string>? Exec(Regex re, string s) => Match(s, re);

    public static int Search(string s, Regex re)
    {
        var m = re.Match(s);
        return m.Success ? m.Index : -1;
    }

    public static T? Pop<T>(List<T> list)
    {
        if (list.Count == 0)
        {
            return default;
        }
        var ret = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
        return ret;
    }

    public static T? Shift<T>(List<T> list)
    {
        if (list.Count == 0)
        {
            return default;
        }
        var ret = list[0];
        list.RemoveAt(0);
        return ret;
    }

    public static int FindIndex<T>(IReadOnlyList<T> list, Func<T, bool> predicate)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (predicate(list[i]))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Array.prototype.indexOf on a read-only list (reference equality for classes, as in JS).</summary>
    public static int IndexOf<T>(IReadOnlyList<T> list, object? item)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (Equals(list[i], item))
            {
                return i;
            }
        }
        return -1;
    }

    public static void ForEach<T>(IEnumerable<T>? list, Action<T> action)
    {
        // x?.forEach(...): nothing to do
        if (list == null)
        {
            return;
        }
        foreach (var x in list.ToList())
        {
            action(x);
        }
    }

    public static List<T> Splice<T>(List<T> list, int start, int? deleteCount = null, params T[] items)
    {
        if (start < 0) start = Math.Max(list.Count + start, 0);
        start = Math.Min(start, list.Count);
        var count = Math.Clamp(deleteCount ?? list.Count - start, 0, list.Count - start);
        var removed = list.GetRange(start, count);
        list.RemoveRange(start, count);
        list.InsertRange(start, items);
        return removed;
    }

    public static List<T> Reverse<T>(List<T> list)
    {
        list.Reverse();
        return list;
    }

    /// <summary>Array.prototype.sort, stable, in place; without a comparer elements are compared as strings.</summary>
    public static List<T> Sort<T>(List<T> list, Func<T, T, int>? compare = null)
    {
        var sorted = compare == null
            ? list.OrderBy(x => JsUtils.Str(x), StringComparer.Ordinal).ToList()
            : list.Select((x, i) => (x, i)).OrderBy(p => p, Comparer<(T x, int i)>.Create((a, b) =>
            {
                var r = compare(a.x, b.x);
                return r != 0 ? r : a.i.CompareTo(b.i);
            })).Select(p => p.x).ToList();
        list.Clear();
        list.AddRange(sorted);
        return list;
    }

    /// <summary>a || b for strings: the empty string is falsy.</summary>
    public static string? Or(string? a, string? b) => string.IsNullOrEmpty(a) ? b : a;

    public static int Or(int a, int b) => a != 0 ? a : b;

    public static int? Or(int? a, int? b) => a != null && a != 0 ? a : b;

    /// <summary>parseInt: leading whitespace, optional sign, leading digits; NaN is returned as null.</summary>
    public static int ParseInt(string? s, int radix = 10)
    {
        var r = ParseIntOrNaN(s, radix);
        return r ?? int.MinValue;
    }

    public static int? ParseIntOrNaN(string? s, int radix = 10)
    {
        if (s == null)
        {
            return null;
        }
        var t = JsString.Trim(s);
        var i = 0;
        var neg = false;
        if (i < t.Length && (t[i] == '+' || t[i] == '-'))
        {
            neg = t[i] == '-';
            i++;
        }
        long value = 0;
        var digits = 0;
        for (; i < t.Length; i++)
        {
            var c = char.ToLowerInvariant(t[i]);
            int d;
            if (c >= '0' && c <= '9') d = c - '0';
            else if (c >= 'a' && c <= 'z') d = c - 'a' + 10;
            else break;
            if (d >= radix) break;
            value = value * radix + d;
            if (value > int.MaxValue) value = int.MaxValue;
            digits++;
        }
        if (digits == 0)
        {
            return null;
        }
        return (int)(neg ? -value : value);
    }

    public static bool IsNaN(int value) => value == int.MinValue;

    public static string TypeOf(object? o)
    {
        return o switch
        {
            null => "undefined",
            string => "string",
            int or long or double or float or decimal => "number",
            bool => "boolean",
            Delegate => "function",
            _ => "object",
        };
    }

    public static bool Truthy(object? o)
    {
        return o switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            int i => i != 0,
            _ => true,
        };
    }

    public static void Noop()
    {
    }

    public static bool IsArray(object? o) => o is IList;
}
