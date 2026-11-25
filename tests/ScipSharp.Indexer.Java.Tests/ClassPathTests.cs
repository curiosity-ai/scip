using ScipSharp.Indexer.Java.Classpath;
using ScipSharp.Indexer.Java.Project;
using ScipSharp.Indexer.Java.Semantics;

namespace ScipSharp.Indexer.Java.Tests;

public class ClassPathTests
{
    [Test]
    public async Task ReadsJdkClassesFromRuntimeImage()
    {
        using var cp = new ClassPath();
        if (!cp.UseJdk(null)) return; // no JDK installed
        var s = cp.Load("java/lang/String");
        await Assert.That(s).IsNotNull();
        await Assert.That(s!.Methods.Any(m => m.Name == "length" && m.Params.Count == 0 && m.Return is PrimT { Name: "int" })).IsTrue();
        await Assert.That(s.Interfaces.OfType<ClassT>().Any(i => i.Name == "java/lang/Comparable")).IsTrue();
        var entry = cp.Load("java/util/Map$Entry");
        await Assert.That(entry!.Outer?.Name).IsEqualTo("java/util/Map");
        await Assert.That(entry.Simple).IsEqualTo("Entry");
        var list = cp.Load("java/util/List");
        await Assert.That(list!.TypeParams.Single()).IsEqualTo("E");
        await Assert.That(cp.PackageExists("java/util")).IsTrue();
    }

    [Test]
    public async Task SignatureParserHandlesGenerics()
    {
        var m = SignatureParser.ParseMethod("<T:Ljava/lang/Object;>(Ljava/util/List<+TT;>;[I)Ljava/util/Map$Entry<TT;*>;");
        await Assert.That(m.TypeParams.Single()).IsEqualTo("T");
        await Assert.That(m.Params.Count).IsEqualTo(2);
        await Assert.That(m.Params[1]).IsEqualTo(new ArrayT(new PrimT("int")));
        await Assert.That(((ClassT)m.Return).Name).IsEqualTo("java/util/Map$Entry");
    }

    [Test]
    public async Task DiscoversGradleAndMavenModules()
    {
        var root = Directory.CreateTempSubdirectory("scipjava-proj").FullName;
        Directory.CreateDirectory(Path.Combine(root, "app", "src", "main", "java"));
        File.WriteAllText(Path.Combine(root, "settings.gradle"), "rootProject.name = 'x'\ninclude 'app'\n");
        File.WriteAllText(Path.Combine(root, "app", "build.gradle"),
            "group = 'org.x'\nversion = '2.0'\ndependencies {\n  implementation 'com.acme:box:2.5'\n}\n");
        var repo = Path.Combine(root, "m2", "com", "acme", "box", "2.5");
        Directory.CreateDirectory(repo);
        File.WriteAllBytes(Path.Combine(repo, "box-2.5.jar"), []);
        var project = new ProjectDiscovery(new ArtifactLocator([Path.Combine(root, "m2")], [])).Discover(root);
        await Assert.That(project.SourceRoots.Count).IsEqualTo(1);
        await Assert.That(project.SourceRoots[0].Package.Version).IsEqualTo("2.0");
        await Assert.That(project.Dependencies.Single().ArtifactId).IsEqualTo("box");
    }

    [Test]
    public async Task ParsesPomWithParentPropertiesAndManagement()
    {
        var dir = Directory.CreateTempSubdirectory("scipjava-pom").FullName;
        var path = Path.Combine(dir, "pom.xml");
        File.WriteAllText(path, """
            <project>
              <parent><groupId>g</groupId><artifactId>parent</artifactId><version>3</version></parent>
              <artifactId>child</artifactId>
              <properties><lib.version>1.2</lib.version></properties>
              <modules><module>a</module></modules>
              <dependencies><dependency><groupId>x</groupId><artifactId>y</artifactId><version>${lib.version}</version></dependency></dependencies>
            </project>
            """);
        var pom = Pom.Load(path)!;
        await Assert.That(pom.EffectiveGroup).IsEqualTo("g");
        await Assert.That(pom.EffectiveVersion).IsEqualTo("3");
        await Assert.That(pom.Resolve(pom.Dependencies.Single().Version)).IsEqualTo("1.2");
        await Assert.That(pom.Modules.Single()).IsEqualTo("a");
    }
}
