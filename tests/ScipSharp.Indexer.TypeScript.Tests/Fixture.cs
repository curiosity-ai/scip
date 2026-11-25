using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ScipSharp.Indexer.TypeScript.Tests;

/// <summary>
/// Prepares the toolchain the end-to-end tests need: Node, a TypeScript install and the npm dependencies of the upstream
/// <c>snapshots/</c> workspace (@types/node, react, ...). Returns a skip reason when something is unavailable.
/// </summary>
public static class Fixture
{
    // Versions of the upstream snapshot dependencies (see .reference/scip-typescript/snapshots/package.json).
    private static readonly string[] Packages =
    [
        "typescript@6.0.3",
        "@types/node@24.19.2",
        "react@19.3.1",
        "@types/react@19.3.0",
        "@sourcegraph/tsconfig@4.0.1",
    ];

    private static readonly Lazy<Task<(string? Root, string? SkipReason)>> Prepared = new(PrepareAsync);

    public static Task<(string? Root, string? SkipReason)> GetAsync() => Prepared.Value;

    /// <summary>Returns the directory containing <c>node_modules</c>, or throws a TUnit skip.</summary>
    public static async Task<string> RequireAsync()
    {
        var (root, skip) = await GetAsync();
        if (root is null)
        {
            Skip.Test(skip ?? "toolchain unavailable");
        }

        return root!;
    }

    public static string? FindUpstreamSnapshots()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, ".reference", "scip-typescript", "snapshots");
            if (Directory.Exists(Path.Combine(candidate, "input")))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? FindOnPath(string fileName)
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, fileName);
            if (File.Exists(candidate)) { return candidate; }
        }

        return null;
    }

    private static async Task<(string? Root, string? SkipReason)> PrepareAsync()
    {
        var node = FindOnPath(OperatingSystem.IsWindows() ? "node.exe" : "node");
        if (node is null)
        {
            return (null, "node is not installed (only needed by tests, to fetch npm type packages via npm)");
        }

        var npmName = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
        var npm = FindOnPath(npmName) ?? Path.Combine(Path.GetDirectoryName(node) ?? "", npmName);
        if (!File.Exists(npm))
        {
            return (null, "npm is not installed");
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', Packages))))[..12].ToLowerInvariant();
        var root = Path.Combine(Path.GetTempPath(), "scip-typescript-dotnet-tests", hash);
        if (File.Exists(Path.Combine(root, "node_modules", "typescript", "lib", "typescript.js")))
        {
            return (root, null);
        }

        Directory.CreateDirectory(root);
        var psi = new ProcessStartInfo(npm) { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in new[] { "install", "--no-audit", "--no-fund", "--prefix", root }.Concat(Packages))
        {
            psi.ArgumentList.Add(a);
        }

        try
        {
            using var p = Process.Start(psi)!;
            var err = p.StandardError.ReadToEndAsync();
            _ = p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            if (p.ExitCode != 0)
            {
                return (null, $"npm install failed (no network access to the npm registry?): {(await err).Trim()}");
            }
        }
        catch (Exception e)
        {
            return (null, "npm install failed: " + e.Message);
        }

        return (root, null);
    }
}
