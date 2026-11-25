using System.Runtime.CompilerServices;

namespace ScipSharp.Indexer.TypeScript.Tests;

/// <summary>
/// Documents whose snapshot output is known to match upstream (excluding references into lib.d.ts and npm packages).
/// A document listed in <c>known-good.txt</c> must keep matching; everything else is a documented gap.
/// Regenerate with <c>SCIP_TS_BASELINE_OUT=path dotnet run</c>.
/// </summary>
public static class Baseline
{
    public static HashSet<string> Load([CallerFilePath] string? thisFile = null)
    {
        var path = Path.Combine(Path.GetDirectoryName(thisFile!)!, "known-good.txt");
        return File.Exists(path) ? [.. File.ReadAllLines(path).Where(l => l.Length > 0)] : [];
    }
}
