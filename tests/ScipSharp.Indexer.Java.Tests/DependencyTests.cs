using System.Diagnostics;
using System.IO.Compression;
using ScipSharp.Indexer.Java.Project;

namespace ScipSharp.Indexer.Java.Tests;

/// <summary>
/// End-to-end test of native classpath reading: a library is compiled with javac (test-only tooling), packaged as a jar in a
/// fake local Maven repository, and the application project referencing it is indexed natively from its <c>pom.xml</c>.
/// </summary>
public class DependencyTests
{
    private static bool? Javac(string dir, string classes, params string[] files)
    {
        var psi = new ProcessStartInfo("javac") { RedirectStandardError = true, RedirectStandardOutput = true };
        psi.ArgumentList.Add("-d");
        psi.ArgumentList.Add(classes);
        foreach (var f in files) psi.ArgumentList.Add(Path.Combine(dir, f));
        try
        {
            using var p = Process.Start(psi)!;
            p.WaitForExit();
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null; // javac is not installed: the test cannot build its fixture
        }
    }

    [Test]
    public async Task ResolvesSymbolsFromMavenDependencyJar()
    {
        var root = Directory.CreateTempSubdirectory("scipjava-dep").FullName;
        var lib = Path.Combine(root, "lib");
        Directory.CreateDirectory(Path.Combine(lib, "p"));
        File.WriteAllText(Path.Combine(lib, "p", "Box.java"), """
            package p;
            import java.util.List;
            public class Box<T> {
              public T value;
              public Box(T value) { this.value = value; }
              public T get() { return value; }
              public <R> List<R> pair(R other) { return null; }
              public static String hello(int n) { return "hi"; }
              public static String hello(String s) { return s; }
              public static class Inner { public void run() {} }
            }
            """);
        var classes = Directory.CreateDirectory(Path.Combine(root, "classes")).FullName;
        var compiled = Javac(lib, classes, Path.Combine("p", "Box.java"));
        if (compiled is null) return;
        await Assert.That(compiled.Value).IsTrue();

        var repo = Path.Combine(root, "m2");
        var artifactDir = Directory.CreateDirectory(Path.Combine(repo, "com", "acme", "box", "2.5")).FullName;
        ZipFile.CreateFromDirectory(classes, Path.Combine(artifactDir, "box-2.5.jar"));
        File.WriteAllText(Path.Combine(artifactDir, "box-2.5.pom"), "<project><groupId>com.acme</groupId><artifactId>box</artifactId><version>2.5</version></project>");

        var app = Directory.CreateDirectory(Path.Combine(root, "app", "src", "main", "java", "q")).FullName;
        File.WriteAllText(Path.Combine(root, "app", "pom.xml"), """
            <project>
              <groupId>org.demo</groupId><artifactId>app</artifactId><version>1.0</version>
              <dependencies>
                <dependency><groupId>com.acme</groupId><artifactId>box</artifactId><version>2.5</version></dependency>
              </dependencies>
            </project>
            """);
        File.WriteAllText(Path.Combine(app, "Main.java"), """
            package q;
            import p.Box;
            public class Main {
              void run() {
                Box<String> b = new Box<>("x");
                String s = b.get();
                int n = s.length();
                Box.hello(1);
                Box.hello("a");
                Box.Inner inner = new Box.Inner();
                inner.run();
              }
            }
            """);

        var project = new ProjectDiscovery(new ArtifactLocator([repo], [])).Discover(Path.Combine(root, "app"));
        var output = Path.Combine(root, "index.scip");
        var rc = await NativeProjectIndexer.RunAsync(project, output, "17", [], new AggregatorReporter(), CancellationToken.None);
        await Assert.That(rc).IsEqualTo(0);
        var doc = Index.Parser.ParseFrom(File.ReadAllBytes(output)).Documents.Single();
        var symbols = doc.Occurrences.Select(o => o.Symbol).ToHashSet();
        const string pkg = "semanticdb maven maven/com.acme/box 2.5 ";
        await Assert.That(symbols.Contains(pkg + "p/Box#")).IsTrue();
        await Assert.That(symbols.Contains(pkg + "p/Box#`<init>`().")).IsTrue();
        await Assert.That(symbols.Contains(pkg + "p/Box#get().")).IsTrue();
        await Assert.That(symbols.Contains(pkg + "p/Box#hello().")).IsTrue();
        await Assert.That(symbols.Contains(pkg + "p/Box#hello(+1).")).IsTrue();
        await Assert.That(symbols.Contains(pkg + "p/Box#Inner#run().")).IsTrue();
        // generic substitution: Box<String>.get() is a String, so length() resolves into the JDK
        await Assert.That(symbols.Contains("semanticdb maven jdk 17 java/lang/String#length().")).IsTrue();
        // project sources belong to the project's own Maven coordinates
        await Assert.That(symbols.Contains("semanticdb maven maven/org.demo/app 1.0 q/Main#")).IsTrue();
    }
}
