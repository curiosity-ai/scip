using System.Text;
using System.Text.Json;

namespace ScipSharp.Indexer.Clang;

/// <summary>One translation unit from <c>compile_commands.json</c>.</summary>
/// <param name="Arguments">Full argument vector; element 0 is the compiler.</param>
public sealed record CompileCommand(string Directory, string File, IReadOnlyList<string> Arguments)
{
    /// <summary>Absolute path of the main source file.</summary>
    public string FullPath => Path.GetFullPath(File, Directory);
}

/// <summary>Reads <c>compile_commands.json</c> (port of upstream <c>CompilationDatabase.cc</c> parsing and shell splitting).</summary>
public static class CompilationDatabase
{
    public static IReadOnlyList<CompileCommand> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("compile_commands.json must be a JSON array");
        }

        var result = new List<CompileCommand>();
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (!entry.TryGetProperty("file", out var file) || !entry.TryGetProperty("directory", out var dir))
            {
                continue;
            }

            IReadOnlyList<string> args;
            if (entry.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Array)
            {
                args = [.. a.EnumerateArray().Select(static x => x.GetString() ?? "")];
            }
            else if (entry.TryGetProperty("command", out var c) && c.GetString() is { } cmd)
            {
                args = SplitCommandLine(cmd);
            }
            else
            {
                continue;
            }

            result.Add(new CompileCommand(dir.GetString()!, file.GetString()!, args));
        }

        return result;
    }

    public static IReadOnlyList<CompileCommand> Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>POSIX-shell style splitting: single quotes are literal, double quotes allow <c>\"</c> and <c>\\</c>, quote characters are removed.</summary>
    public static List<string> SplitCommandLine(string command)
    {
        var args = new List<string>();
        var cur = new StringBuilder();
        var inToken = false;
        for (var i = 0; i < command.Length; i++)
        {
            var ch = command[i];
            switch (ch)
            {
                case '\'':
                    inToken = true;
                    for (i++; i < command.Length && command[i] != '\''; i++)
                    {
                        cur.Append(command[i]);
                    }

                    break;
                case '"':
                    inToken = true;
                    for (i++; i < command.Length && command[i] != '"'; i++)
                    {
                        if (command[i] == '\\' && i + 1 < command.Length && command[i + 1] is '"' or '\\')
                        {
                            i++;
                        }

                        cur.Append(command[i]);
                    }

                    break;
                case '\\' when i + 1 < command.Length:
                    inToken = true;
                    cur.Append(command[++i]);
                    break;
                case ' ' or '\t' or '\n' or '\r':
                    if (inToken)
                    {
                        args.Add(cur.ToString());
                        cur.Clear();
                        inToken = false;
                    }

                    break;
                default:
                    inToken = true;
                    cur.Append(ch);
                    break;
            }
        }

        if (inToken)
        {
            args.Add(cur.ToString());
        }

        return args;
    }
}
