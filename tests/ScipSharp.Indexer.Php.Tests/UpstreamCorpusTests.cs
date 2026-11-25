using System.Text;
using ScipSharp.Indexer.Php.Syntax;

namespace ScipSharp.Indexer.Php.Tests;

public class UpstreamCorpusTests
{
    /// <summary>
    /// Parses upstream's own PHP sources (modern PHP 8.3 code) when the read-only <c>.reference/scip-php</c> checkout is
    /// reachable from the test directory or <c>SCIP_PHP_UPSTREAM_DIR</c> is set; otherwise the test is a no-op.
    /// </summary>
    [Test]
    public async Task ParsesUpstreamScipPhpSources()
    {
        var dir = Environment.GetEnvironmentVariable("SCIP_PHP_UPSTREAM_DIR");
        if (string.IsNullOrEmpty(dir))
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            {
                var candidate = Path.Combine(d.FullName, ".reference", "scip-php");
                if (Directory.Exists(candidate))
                {
                    dir = candidate;
                    break;
                }
            }
        }

        if (dir == null || !Directory.Exists(dir))
        {
            return;
        }

        var failures = new List<string>();
        var count = 0;
        foreach (var f in Directory.EnumerateFiles(dir, "*.php", SearchOption.AllDirectories))
        {
            var text = Encoding.Latin1.GetString(File.ReadAllBytes(f));
            if (text.Length == 0)
            {
                continue;
            }

            count++;
            try
            {
                PhpParser.Parse(text);
            }
            catch (ParseException e)
            {
                failures.Add($"{f}: {e.Message}");
            }
        }

        await Assert.That(count).IsGreaterThan(0);
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", failures));
        }
    }

    private static string? PhpExecutable()
    {
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, "php");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Cross-checks the lexer's PHP-compatible token numbering (which anonymous class / function names depend on) against
    /// PHP's own tokenizer. Test-only use of an external toolchain: skipped when <c>php</c> is not installed.
    /// </summary>
    [Test]
    public async Task TokenCountsMatchPhpTokenizer()
    {
        var php = PhpExecutable();
        if (php == null)
        {
            return;
        }

        var script = Path.Combine(Path.GetTempPath(), "scip-php-count-" + Guid.NewGuid().ToString("N") + ".php");
        await File.WriteAllTextAsync(script, "<?php echo count(PhpToken::tokenize(file_get_contents($argv[1])));");
        try
        {
            var mismatches = new List<string>();
            var files = Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "testdata"), "*.php", SearchOption.AllDirectories)
                .Where(f => new FileInfo(f).Length > 0);
            var extra = Environment.GetEnvironmentVariable("SCIP_PHP_UPSTREAM_DIR");
            if (!string.IsNullOrEmpty(extra) && Directory.Exists(extra))
            {
                files = files.Concat(Directory.EnumerateFiles(extra, "*.php", SearchOption.AllDirectories).Where(f => new FileInfo(f).Length > 0));
            }

            foreach (var f in files)
            {
                var psi = new System.Diagnostics.ProcessStartInfo(php) { RedirectStandardOutput = true, RedirectStandardError = true };
                psi.ArgumentList.Add(script);
                psi.ArgumentList.Add(f);
                using var proc = System.Diagnostics.Process.Start(psi)!;
                var output = (await proc.StandardOutput.ReadToEndAsync()).Trim();
                await proc.WaitForExitAsync();
                if (!int.TryParse(output, out var expected))
                {
                    continue;
                }

                var tokens = new Lexer(Encoding.Latin1.GetString(File.ReadAllBytes(f))).Tokenize();
                if (tokens[^1].Pos != expected)
                {
                    mismatches.Add($"{Path.GetFileName(f)}: php={expected} ours={tokens[^1].Pos}");
                }
            }

            if (mismatches.Count > 0)
            {
                throw new InvalidOperationException(string.Join("\n", mismatches));
            }
        }
        finally
        {
            File.Delete(script);
        }
    }
}
