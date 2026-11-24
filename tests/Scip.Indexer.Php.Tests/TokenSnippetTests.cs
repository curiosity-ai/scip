using System.Diagnostics;
using System.Text;
using Scip.Indexer.Php.Syntax;

namespace Scip.Indexer.Php.Tests;

/// <summary>
/// Compares the lexer's PHP-compatible token numbering with PHP's tokenizer on tricky snippets. This is a test-only use
/// of an external toolchain: it is skipped silently when <c>php</c> is not on the PATH.
/// </summary>
public class TokenSnippetTests
{
    private static readonly string[] Snippets =
    [
        "<?php\n$a = \"x $b y {$c->d} $e[0] $f[k] $g->h ${i}\";\n",
        "<?php\n$a = <<<EOT\n  Hello $name {$o->p()}\n  more\n  EOT;\n$b = <<<'RAW'\nraw $x\nRAW;\n",
        "<?php\n$a = (int) $b + (string)$c;\n// comment\n# hash\n/* block */ /** doc */ $x = 1_000 + 0x1F + 1.5e3;\n",
        "<html><?php if ($a): ?>\n  <b><?= $x ?></b>\n<?php endif; ?>\ntail\n",
        "<?php\n#[Attr(1)] function f(?int $a = null, int|string ...$r): static|null { return $a?->b ?? $c::D; }\n",
        "<?php\n$a = `ls $b`; $c = 'it\\'s'; $d = \"q\\\"uote\"; namespace\\foo(); \\A\\B::c();\n",
    ];

    private static string? PhpExecutable() =>
        (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => Path.Combine(d, "php"))
            .FirstOrDefault(File.Exists);

    [Test]
    public async Task TokenCountsMatchPhp()
    {
        var php = PhpExecutable();
        if (php == null)
        {
            return;
        }

        var mismatches = new List<string>();
        foreach (var snippet in Snippets)
        {
            var file = Path.Combine(Path.GetTempPath(), "tok-" + Guid.NewGuid().ToString("N") + ".php");
            await File.WriteAllTextAsync(file, snippet, Encoding.Latin1);
            try
            {
                var psi = new ProcessStartInfo(php) { RedirectStandardOutput = true, RedirectStandardError = true };
                psi.ArgumentList.Add("-r");
                psi.ArgumentList.Add("echo count(PhpToken::tokenize(file_get_contents($argv[1])));");
                psi.ArgumentList.Add(file);
                using var proc = Process.Start(psi)!;
                var output = (await proc.StandardOutput.ReadToEndAsync()).Trim();
                await proc.WaitForExitAsync();
                if (!int.TryParse(output, out var expected))
                {
                    continue;
                }

                var ours = new Lexer(snippet).Tokenize()[^1].Pos;
                if (ours != expected)
                {
                    mismatches.Add($"php={expected} ours={ours}: {snippet.Replace("\n", "\\n", StringComparison.Ordinal)}");
                }
            }
            finally
            {
                File.Delete(file);
            }
        }

        if (mismatches.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", mismatches));
        }
    }
}
