namespace Scip.Indexer.Java.Tests;

public class BuildToolTests
{
    [Test]
    public async Task FixJavacOptionsDropsProblematicFlags()
    {
        var fixedOpts = JavacBuildTool.FixJavacOptions(
            ["--release", "11", "-Xep:Foo", "-XDcompilePolicy=simple", "-Xplugin:scip -targetroot:x", "-parameters"]);
        await Assert.That(string.Join("|", fixedOpts)).IsEqualTo("-parameters");
    }

    [Test]
    public async Task ParsesConfig()
    {
        var c = JavaProjectConfig.Parse("""{"javaHome":"/jdk","classpath":["a.jar"],"sourceFiles":["src"],"reportWarningOnEmptyIndex":false}""");
        await Assert.That(c.JavaHome).IsEqualTo("/jdk");
        await Assert.That(c.Classpath.Single()).IsEqualTo("a.jar");
        await Assert.That(c.ReportWarningOnEmptyIndex).IsFalse();
    }

    [Test]
    public async Task BuildsPluginArguments()
    {
        var root = Directory.CreateTempSubdirectory("scipjava").FullName;
        var tool = new JavacBuildTool(root, Path.Combine(root, "t"), "/p/plugin.jar", JavaProjectConfig.Empty, false);
        var args = tool.BuildArguments(["/x/A.java"], "/c", "/g");
        await Assert.That(args).Contains($"-Xplugin:scip -targetroot:{Path.Combine(root, "t")} -sourceroot:{root}");
        await Assert.That(args[^1]).IsEqualTo("/x/A.java");
    }

    [Test]
    public async Task CollectsSourcesSkippingModuleInfoAndTargetRoot()
    {
        var root = Directory.CreateTempSubdirectory("scipjava").FullName;
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "t"));
        File.WriteAllText(Path.Combine(root, "src", "A.java"), "");
        File.WriteAllText(Path.Combine(root, "src", "module-info.java"), "");
        File.WriteAllText(Path.Combine(root, "t", "Gen.java"), "");
        var tool = new JavacBuildTool(root, Path.Combine(root, "t"), "p.jar", JavaProjectConfig.Empty, false);
        await Assert.That(string.Join("|", tool.CollectJavaSources().Select(Path.GetFileName))).IsEqualTo("A.java");
    }

    [Test]
    public async Task ShardPathsMatchUpstreamLayout()
    {
        await Assert.That(ShardPaths.ShardPath("/t", "/src", "/src/a/B.java")).IsEqualTo(Path.Combine("/t", "META-INF", "scip", "a", "B.java.scip"));
        await Assert.That(ShardPaths.ShardPath("/t", "/src", "/other/B.java")).IsNull();
        await Assert.That(ShardPaths.RelativePath("/src", "/src/a/B.java")).IsEqualTo("a/B.java");
    }

    [Test]
    public async Task ReadsDependenciesAndPoms()
    {
        var dir = Directory.CreateTempSubdirectory("scipjava").FullName;
        File.WriteAllText(Path.Combine(dir, "x-dependencies.txt"), "g\ta\t1\t/p/a.jar\nbad line\n");
        var pkgs = ClasspathEntries.FromTargetRoot(dir, dir);
        await Assert.That(pkgs.Single()).IsEqualTo(new MavenPackage("/p/a.jar", "g", "a", "1"));

        var pom = Path.Combine(dir, "pom.xml");
        File.WriteAllText(pom, "<project><parent><groupId>pg</groupId><version>2</version></parent><artifactId>art</artifactId></project>");
        await Assert.That(ClasspathEntries.FromPom(pom, "e")).IsEqualTo(new MavenPackage("e", "pg", "art", "2"));
    }

    [Test]
    public async Task ParsesIndexerArguments()
    {
        var a = JavaIndexerArguments.Parse(["aggregate", "--targetroot", "t", "--no-inverse-relationships"]);
        await Assert.That(a.AggregateOnly).IsTrue();
        await Assert.That(a.TargetRoot).IsEqualTo("t");
        await Assert.That(a.NoInverse).IsTrue();
        await Assert.That(JavaIndexerArguments.Parse(["--bogus"]).Error).IsNotNull();
    }
}
