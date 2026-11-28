using System.Text;
using ScipSharp.Indexing;

namespace ScipSharp.Indexer.Rust.Tests;

/// <summary>Malformed input must not abort indexing; pathological (but valid) input must not hang or overflow the stack.</summary>
public class RobustnessTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(20);

    private static Dictionary<string, string> Crate(params (string Path, string Content)[] files) =>
        ProjectFixture.Workspace([("Cargo.toml", ProjectFixture.Manifest("t", "0.1.0")), .. files]);

    /// <summary>Indexes on a worker thread with a small stack (so stack guards are exercised) within <see cref="Limit"/>.</summary>
    private static async Task<Index> IndexWithin(Dictionary<string, string> files, int stackSize = 1024 * 1024)
    {
        Index? index = null;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                // synchronous on this thread (RustIndexer.IndexAsync completes synchronously)
                var dir = Directory.CreateTempSubdirectory("scip-rust-robust-").FullName;
                foreach (var (path, content) in files)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(dir, path))!);
                    File.WriteAllText(Path.Combine(dir, path), content);
                }

                var output = Path.Combine(dir, "index.scip");
                if (new RustIndexer().IndexAsync(new IndexerOptions(dir, output, []), CancellationToken.None).GetAwaiter().GetResult() != 0)
                {
                    throw new InvalidOperationException("indexing failed");
                }

                index = Index.Parser.ParseFrom(File.ReadAllBytes(output));
            }
            catch (Exception e)
            {
                error = e;
            }
        }, stackSize) { IsBackground = true };
        thread.Start();
        await Assert.That(await Task.Run(() => thread.Join(Limit))).IsTrue();
        return error is null ? index! : throw error;
    }

    [Test]
    public async Task TruncatedFilesDoNotAbortIndexing()
    {
        string[] broken = ["impl S { pub fn", "pub fn f() { let c = |x", "pub", "pub fn f() { /* unterminated", "fn f() { let s = r#\"abc", "use a::{b, c"];
        var files = Crate(
            ("src/lib.rs", string.Concat(broken.Select((_, i) => $"pub mod b{i};\n")) + "pub mod good;\n"),
            ("src/good.rs", "pub fn fine() {}\n"));
        for (var i = 0; i < broken.Length; i++)
        {
            files[$"src/b{i}.rs"] = broken[i];
        }

        var index = await IndexWithin(files);
        await Assert.That(index.Documents.Single(d => d.RelativePath == "src/good.rs").Symbols.Any(s => s.Symbol.EndsWith("fine().", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task DeepNestingIsCutOffInsteadOfOverflowingTheStack()
    {
        const int depth = 3000; // far deeper than a 256 KB stack allows without the guards
        var source = new StringBuilder();
        source.Append(string.Concat(Enumerable.Repeat("pub mod a { ", depth))).Append("pub fn f() {}").Append(string.Concat(Enumerable.Repeat(" }", depth))).Append('\n');
        source.Append("pub fn g() { let x = ").Append(string.Concat(Enumerable.Repeat("Some(", depth))).Append(1).Append(')', depth).Append("; }\n");
        var index = await IndexWithin(Crate(("src/lib.rs", source.ToString())), 256 * 1024);
        await Assert.That(index.Documents.Single().Symbols.Any(s => s.Symbol.EndsWith("g().", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task LargeFilesAndGlobImportGraphsStayLinear()
    {
        // thousands of generic items and impls in one file (per-identifier scans of all scopes / impl ranges were
        // quadratic), a big match (arm regions), and a glob-import graph walked for every unresolved name
        const int n = 2000; // before the fixes: ~6 s; after: well under 1 s (plus the fixed std-model setup)
        var lib = new StringBuilder("pub struct S;\npub trait T<const N: usize> { fn m(&self); }\n");
        for (var i = 0; i < n; i++)
        {
            lib.Append($"pub fn f{i}<A: Clone>(x: A, y: u32) -> A {{ let z = y + 1; x.clone() }}\n");
            lib.Append($"impl T<{i}> for S {{ fn m(&self) {{}} }}\n");
            lib.Append($"pub mod m{i} {{ pub use crate::*; pub use super::*; pub use crate::m{(i + 1) % n}::*; pub struct S{i}; pub fn h() {{ let _ = Missing{i}; }} }}\n");
        }

        lib.Append("pub fn big(x: u32) -> u32 { match x {\n");
        for (var i = 0; i < n; i++)
        {
            lib.Append($"  {i} => {{ let a = x + {i}; a }}\n");
        }

        lib.Append("  _ => 0 } }\n");
        var index = await IndexWithin(Crate(("src/lib.rs", lib.ToString())));
        await Assert.That(index.Documents.Single().Symbols.Any(s => s.Symbol.EndsWith("big().", StringComparison.Ordinal))).IsTrue();
    }
}
