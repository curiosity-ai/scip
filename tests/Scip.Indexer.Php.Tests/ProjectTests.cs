using System.Text;

namespace Scip.Indexer.Php.Tests;

/// <summary>End-to-end tests on small throw-away composer projects (no vendor directory needed).</summary>
public class ProjectTests
{
    private sealed class TempProject : IDisposable
    {
        public TempProject(Dictionary<string, string> files)
        {
            Dir = Path.Combine(Path.GetTempPath(), "scip-php-proj-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Dir);
            foreach (var (name, content) in files)
            {
                var p = Path.Combine(Dir, name);
                Directory.CreateDirectory(Path.GetDirectoryName(p)!);
                File.WriteAllText(p, content);
            }
        }

        public string Dir { get; }

        public static Dictionary<string, string> WithVendor(Dictionary<string, string> files)
        {
            files["vendor/composer/installed.php"] =
                "<?php return array('root' => array('name' => 'acme/app', 'version' => 'dev', 'reference' => 'dev'), 'versions' => array());";
            files["vendor/composer/autoload_psr4.php"] =
                "<?php $vendorDir = dirname(__DIR__); $baseDir = dirname($vendorDir); return array('App\\\\' => array($baseDir . '/src'));";
            return files;
        }

        public void Dispose() => Directory.Delete(Dir, recursive: true);
    }

    private const string ComposerJson = """
        { "name": "acme/app", "autoload": { "psr-4": { "App\\": "src" }, "classmap": ["src"] } }
        """;

    private static (Index Index, List<string> Warnings) Run(TempProject p)
    {
        var warnings = new List<string>();
        return (new PhpProjectIndexer(p.Dir, "t", [], "8.4.0", warnings.Add).Build(), warnings);
    }

    private static List<string> Rendered(Index index, TempProject p, string rel)
    {
        var doc = index.Documents.Single(d => d.RelativePath == rel);
        var src = Encoding.Latin1.GetString(File.ReadAllBytes(Path.Combine(p.Dir, rel)));
        return [.. SnapshotFormatter.Format(doc, src).Split('\n')];
    }

    private static string Short(string line) => line.Replace("scip-php composer acme/app dev ", "", StringComparison.Ordinal);

    [Test]
    public async Task ResolvesUseAliasesInterpolationAndPromotion()
    {
        using var p = new TempProject(TempProject.WithVendor(new()
        {
            ["composer.json"] = ComposerJson,
            ["src/Thing.php"] = """
                <?php
                namespace App;

                interface HasName { public function name(): string; }

                class Thing implements HasName
                {
                    public const MAX = 3;
                    public function __construct(public string $name = 'x', private ?Thing $next = null) {}
                    public function name(): string { return "n={$this->name} {$this->next?->name()} $this->name"; }
                    public static function make(): static { return new static(); }
                }
                """,
            ["src/Use.php"] = """
                <?php
                namespace App\Sub;

                use App\Thing as T;
                use App\{HasName};
                use function strlen;

                final class User
                {
                    public function run(T $t, HasName $h): int
                    {
                        $f = fn(int $x) => $x + T::MAX;
                        $g = function (string $s) use ($t) { return strlen($s) + $t::MAX; };
                        $n = T::make()->name();
                        return $f(1) + $t->name() + count([]) + ($t instanceof T ? 1 : 0);
                    }
                }
                """,
        }));

        var (index, warnings) = Run(p);
        await Assert.That(warnings).IsEmpty();

        var thing = string.Join("\n", Rendered(index, p, "src/Thing.php").Select(Short));
        await Assert.That(thing).Contains("definition App/Thing#$name.");
        await Assert.That(thing).Contains("definition App/Thing#$next.");
        await Assert.That(thing).Contains("definition App/Thing#MAX.");
        await Assert.That(thing).Contains("reference App/HasName#");
        // Property fetches inside string interpolation are indexed (3 usages of `name`, 1 of the nullsafe method call).
        await Assert.That(CountLines(thing, "reference App/Thing#$name.")).IsEqualTo(2);
        await Assert.That(CountLines(thing, "reference App/Thing#name().")).IsEqualTo(1);

        var use = string.Join("\n", Rendered(index, p, "src/Use.php").Select(Short));
        await Assert.That(use).Contains("reference App/Thing#MAX.");
        await Assert.That(use).Contains("reference App/Thing#make().");
        await Assert.That(use).Contains("reference scip-php composer php 8.4.0 strlen().");
        await Assert.That(use).Contains("reference scip-php composer php 8.4.0 count().");
        await Assert.That(use).Contains("definition App/Sub/anon-func-");
        await Assert.That(use).Contains("run().($t)");
    }

    private static int CountLines(string text, string needle) =>
        text.Split('\n').Count(l => l.Contains(needle, StringComparison.Ordinal));

    [Test]
    public async Task IndexesTraitsEnumsAndInheritedMembers()
    {
        using var p = new TempProject(TempProject.WithVendor(new()
        {
            ["composer.json"] = ComposerJson,
            ["src/Model.php"] = """
                <?php
                namespace App;

                trait Greets { public function hello(): string { return 'hi'; } }

                enum Level: int
                {
                    case Low = 1;
                    case High = 2;
                    public static function top(): self { return self::High; }
                }

                abstract class Base { protected int $count = 0; abstract public function id(): int; }

                final class Impl extends Base
                {
                    use Greets;
                    public function id(): int { return $this->count + strlen($this->hello()) + Level::Low->value; }
                }
                """,
        }));
        var (index, warnings) = Run(p);
        await Assert.That(warnings).IsEmpty();
        var text = string.Join("\n", Rendered(index, p, "src/Model.php").Select(Short));
        await Assert.That(text).Contains("definition App/Level#Low.");
        await Assert.That(text).Contains("reference App/Level#High.");
        await Assert.That(text).Contains("reference App/Base#$count.");
        await Assert.That(text).Contains("reference App/Greets#hello().");
        await Assert.That(text).Contains("reference App/Greets#");
        await Assert.That(text).Contains("> abstract class Base");
        await Assert.That(text).Contains("> enum Level");
        await Assert.That(text).Contains("> case Low = 1;");
        await Assert.That(text).Contains("> protected int $count = 0");
    }

    [Test]
    public async Task UnparsableFileIsReportedAndSkipped()
    {
        using var p = new TempProject(TempProject.WithVendor(new()
        {
            ["composer.json"] = ComposerJson,
            ["src/Good.php"] = "<?php\nnamespace App;\nclass Good {}\n",
            ["src/Bad.php"] = "<?php\nnamespace App;\nclass Bad { function ( }\n",
        }));
        var (index, warnings) = Run(p);
        await Assert.That(index.Documents.Select(d => d.RelativePath).ToArray()).IsEquivalentTo(["src/Good.php"]);
        await Assert.That(warnings.Any(w => w.Contains("Bad.php", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task MissingComposerJsonIsAnError()
    {
        var dir = Path.Combine(Path.GetTempPath(), "scip-php-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var code = await new PhpIndexer().IndexAsync(new Scip.Indexing.IndexerOptions(dir, Path.Combine(dir, "out.scip"), []), CancellationToken.None);
            await Assert.That(code).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
