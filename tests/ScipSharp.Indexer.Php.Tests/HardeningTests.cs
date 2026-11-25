using System.Text;

namespace ScipSharp.Indexer.Php.Tests;

/// <summary>Regressions found by indexing laravel, symfony, phpmyadmin and WordPress: non-UTF-8 names, anonymous classes, lint cleanliness.</summary>
public class HardeningTests
{
    private const string ComposerJson = """{ "name": "acme/app", "autoload": { "psr-4": { "App\\": "src" } } }""";

    private static string NewDir(Dictionary<string, byte[]> files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-php-hard-" + Guid.NewGuid().ToString("N"));
        foreach (var (name, content) in files)
        {
            var p = Path.Combine(dir, name);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllBytes(p, content);
        }

        return dir;
    }

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    private static Index Build(string dir) => new PhpProjectIndexer(dir, "t", [], "8.4.0").Build();

    /// <summary>The checks of <c>scip lint</c> that matter here: valid symbols, and SymbolInformation for every occurrence, exactly once.</summary>
    private static List<string> Lint(Index index)
    {
        var errors = new List<string>();
        var known = new HashSet<string>();
        foreach (var info in index.Documents.SelectMany(d => d.Symbols).Concat(index.ExternalSymbols))
        {
            if (!known.Add(info.Symbol))
            {
                errors.Add("duplicate info " + info.Symbol);
            }

            if (SymbolParser.Validate(info.Symbol) is { } bad)
            {
                errors.Add(bad);
            }
        }

        foreach (var occ in index.Documents.SelectMany(d => d.Occurrences))
        {
            if (!occ.Symbol.StartsWith("local ", StringComparison.Ordinal) && !known.Contains(occ.Symbol))
            {
                errors.Add("no info " + occ.Symbol);
            }
        }

        return errors;
    }

    [Test]
    public async Task LatinOneIdentifiersAreEscapedNotReplacementCharacters()
    {
        var dir = NewDir(new()
        {
            ["composer.json"] = Utf8(ComposerJson),
            // Valid UTF-8 non-ASCII names, and a Latin-1 file whose identifier bytes are not valid UTF-8.
            ["src/Utf.php"] = Utf8("<?php\nnamespace App;\nclass Utf { public function émail(): void {} public function f(string $routeÀ) { $this->émail(); } }\n"),
            ["src/Latin.php"] = [.. Utf8("<?php\nnamespace App;\nclass Latin { const "), 0xE9, .. Utf8("té = 1; public function f() { return self::"), 0xE9, .. Utf8("té; } }\n")],
        });
        try
        {
            var index = Build(dir);
            await Assert.That(Lint(index)).IsEmpty();
            var symbols = index.Documents.SelectMany(d => d.Symbols).Select(s => s.Symbol).ToList();
            await Assert.That(symbols.Any(s => s.Contains('�'))).IsFalse();
            await Assert.That(symbols.Any(s => s.EndsWith("Utf#`émail`().", StringComparison.Ordinal))).IsTrue();

            // Positions stay UTF-8 byte offsets: the reference `émail` after `$this->` is 2 bytes wide.
            var utf = index.Documents.Single(d => d.RelativePath == "src/Utf.php");
            var refOcc = utf.Occurrences.Single(o => o.Symbol.EndsWith("Utf#`émail`().", StringComparison.Ordinal) && o.SymbolRoles == 0);
            var r = refOcc.SingleLineRange;
            await Assert.That(r.EndCharacter - r.StartCharacter).IsEqualTo(6); // é (2 bytes) + mail
            await Assert.That(index.Metadata.TextDocumentEncoding).IsEqualTo(TextEncoding.Utf8);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task AnonymousClassesAndCaseInsensitiveFunctionsHaveSymbolInformation()
    {
        var dir = NewDir(new()
        {
            ["composer.json"] = Utf8(ComposerJson),
            ["src/Anon.php"] = Utf8("""
                <?php
                namespace App;

                use Event;

                function event() {}

                class Anon
                {
                    public function run()
                    {
                        $a = new class {
                            public function name(): string { return static::class; }
                            public function other() { return $this->name(); }
                        };
                        Event::listen('x');
                        return Event(1) + $a->name();
                    }
                }
                """),
        });
        try
        {
            var index = Build(dir);
            await Assert.That(Lint(index)).IsEmpty();
            var symbols = index.Documents.SelectMany(d => d.Symbols).Select(s => s.Symbol).ToList();
            await Assert.That(symbols.Any(s => s.Contains("anon-class-", StringComparison.Ordinal) && s.EndsWith('#'))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task ProjectDefinedPolyfillIsNotAlsoExternal()
    {
        var dir = NewDir(new()
        {
            ["composer.json"] = Utf8("""{ "name": "acme/app", "autoload": { "files": ["compat.php", "use.php"] } }"""),
            ["compat.php"] = Utf8("<?php\nif (!function_exists('str_contains')) { function str_contains($h, $n) { return true; } }\n"),
            ["use.php"] = Utf8("<?php\nfunction g() { return str_contains('a', 'b'); }\n"),
        });
        try
        {
            await Assert.That(Lint(Build(dir))).IsEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task ProjectWithoutComposerJsonIndexesAllPhpFiles()
    {
        var dir = NewDir(new()
        {
            ["index.php"] = Utf8("<?php\nrequire 'inc/f.php';\nhelper();\n"),
            ["inc/f.php"] = Utf8("<?php\nfunction helper() {}\n"),
            ["vendor/x/y.php"] = Utf8("<?php\nfunction ignored() {}\n"),
        });
        try
        {
            var index = Build(dir);
            await Assert.That(index.Documents.Select(d => d.RelativePath).Order().ToList()).IsEquivalentTo(["inc/f.php", "index.php"]);
            await Assert.That(Lint(index)).IsEmpty();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public async Task ParsesPhp84AsymmetricVisibilityNestedTernaryAndNestedInterpolation()
    {
        var dir = NewDir(new()
        {
            ["composer.json"] = Utf8(ComposerJson),
            ["src/Modern.php"] = Utf8("""
                <?php
                namespace App;

                class Modern
                {
                    public private(set) string $slug = '';
                    protected(set) readonly array $list;
                    public function __construct(public protected(set) ?int $id = null) {}

                    public function f($host, $file, $wait)
                    {
                        $a = $file ? realpath($file) ?: null : null;
                        $b = "wait {$this->g("{$wait}s")} done {$this->slug}";
                        return [$a, $b];
                    }

                    public function g(string $s): string { return $s; }
                }
                """),
        });
        try
        {
            var warnings = new List<string>();
            var index = new PhpProjectIndexer(dir, "t", [], "8.4.0", warnings.Add).Build();
            await Assert.That(warnings.Where(w => !w.Contains("installed.php", StringComparison.Ordinal))).IsEmpty();
            await Assert.That(index.Documents).HasSingleItem();
            await Assert.That(Lint(index)).IsEmpty();
            var occ = index.Documents[0].Occurrences.Select(o => o.Symbol).ToList();
            await Assert.That(occ.Any(s => s.EndsWith("Modern#g().", StringComparison.Ordinal))).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
