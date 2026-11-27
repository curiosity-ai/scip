namespace ScipSharp.Indexer.Vb6.Tests;

public class ResolutionTests
{
    /// <summary>(line text, symbol) of every reference in <paramref name="doc"/>, rendered as <c>name→symbol</c>.</summary>
    private static List<string> References(Index index, string path, string sourceText)
    {
        var lines = sourceText.ReplaceLineEndings("\n").Split('\n');
        var doc = index.Documents.Single(d => d.RelativePath == path);
        return doc.Occurrences
            .Where(o => (o.SymbolRoles & (int)SymbolRole.Definition) == 0)
            .Select(o =>
            {
                o.TryGetSourceRange(out var r);
                return lines[r.Start.Line][r.Start.Character..r.End.Character] + "→" + o.Symbol.Replace("scip-vb6 vbp ", "", StringComparison.Ordinal);
            })
            .ToList();
    }

    [Test]
    public async Task LocalsShadowModuleAndProjectMembers()
    {
        const string bas = """
            Attribute VB_Name = "M"
            Public Count As Long
            Sub A()
                Dim Count As Long
                Count = 1
            End Sub
            Sub B()
                Count = 2
                M.Count = 3
            End Sub
            """;
        using var dir = TestSupport.Files(("M.bas", bas));
        var index = TestSupport.Build(dir.Path);
        var refs = References(index, "M.bas", bas);
        var dirName = Path.GetFileName(dir.Path);
        await Assert.That(refs[0]).IsEqualTo("Count→local 0");
        await Assert.That(refs[1]).IsEqualTo($"Count→{dirName} . M#Count.");
        await Assert.That(refs[2]).IsEqualTo($"M→{dirName} . M#");
        await Assert.That(refs[3]).IsEqualTo($"Count→{dirName} . M#Count.");
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task ResolvesCaseInsensitivelyAcrossModulesAndUsesDeclaredSpelling()
    {
        const string a = """
            Attribute VB_Name = "Utils"
            Public Function DoubleIt(ByVal x As Long) As Long
                DoubleIt = x * 2
            End Function
            Private Sub Hidden()
            End Sub
            """;
        const string b = """
            Attribute VB_Name = "Main"
            Sub Go()
                Debug.Print DOUBLEIT(2), utils.doubleit(3)
                Hidden
            End Sub
            """;
        using var dir = TestSupport.Files(("Utils.bas", a), ("Main.bas", b));
        var index = TestSupport.Build(dir.Path);
        var refs = References(index, "Main.bas", b);
        await Assert.That(refs.Count).IsEqualTo(3); // Private Hidden is not visible from Main
        await Assert.That(refs.All(r => r.EndsWith("Utils#DoubleIt().", StringComparison.Ordinal) || r.EndsWith("Utils#", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task TypesMemberAccessThroughDeclarationsWithAndFunctions()
    {
        const string cls = """
            VERSION 1.0 CLASS
            BEGIN
              MultiUse = -1  'True
            END
            Attribute VB_Name = "Node"
            Public Value As Long
            Public NextNode As Node
            Public Function Child(ByVal i As Long) As Node
            End Function
            """;
        const string bas = """
            Attribute VB_Name = "Walk"
            Public Type Pair
                Left As Node
            End Type
            Sub Go(p As Pair)
                Dim n As New Node
                n.NextNode.Value = 1
                n.Child(1).Child(2).Value = 2
                With p.Left
                    .Value = .NextNode.Value
                    With .NextNode
                        .Value = 3
                    End With
                End With
                Set n = Me.Nothing
            End Sub
            """;
        using var dir = TestSupport.Files(("Node.cls", cls), ("Walk.bas", bas));
        var index = TestSupport.Build(dir.Path);
        var resolved = References(index, "Walk.bas", bas).Select(r => r.Split('→')[0] + "→" + r[(r.LastIndexOf(' ') + 1)..]).ToList();
        await Assert.That(resolved).IsEquivalentTo([
            "Node→Node#", "Pair→Walk#Pair#", "Node→Node#",
            "n→1", "NextNode→Node#NextNode.", "Value→Node#Value.",
            "n→1", "Child→Node#Child().", "Child→Node#Child().", "Value→Node#Value.",
            "p→0", "Left→Walk#Pair#Left.",
            "Value→Node#Value.", "NextNode→Node#NextNode.", "Value→Node#Value.",
            "NextNode→Node#NextNode.", "Value→Node#Value.",
            "n→1",
        ]);
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task BindsEventHandlersAndInterfaceImplementations()
    {
        const string iface = """
            VERSION 1.0 CLASS
            BEGIN
              MultiUse = -1  'True
            END
            Attribute VB_Name = "IGreeter"
            Public Sub Greet(ByVal name As String)
            End Sub
            """;
        const string source = """
            VERSION 1.0 CLASS
            BEGIN
              MultiUse = -1  'True
            END
            Attribute VB_Name = "Ticker"
            Public Event Tick(ByVal n As Long)
            Implements IGreeter
            Private Sub IGreeter_Greet(ByVal name As String)
                RaiseEvent Tick(1)
            End Sub
            """;
        const string listener = """
            VERSION 1.0 CLASS
            BEGIN
              MultiUse = -1  'True
            END
            Attribute VB_Name = "Listener"
            Private WithEvents m_ticker As Ticker
            Private Sub m_ticker_Tick(ByVal n As Long)
            End Sub
            """;
        using var dir = TestSupport.Files(("IGreeter.cls", iface), ("Ticker.cls", source), ("Listener.cls", listener));
        var index = TestSupport.Build(dir.Path);
        var infos = index.Documents.SelectMany(d => d.Symbols).Where(s => !s.Symbol.StartsWith("local ", StringComparison.Ordinal)).ToDictionary(s => s.Symbol[(s.Symbol.LastIndexOf(' ') + 1)..]);
        await Assert.That(infos["Ticker#"].Relationships.Single().Symbol).EndsWith(" IGreeter#");
        await Assert.That(infos["Ticker#"].Relationships.Single().IsImplementation).IsTrue();
        var greet = infos["Ticker#IGreeter_Greet()."].Relationships.Single();
        await Assert.That(greet.Symbol).EndsWith(" IGreeter#Greet().");
        await Assert.That(greet.IsImplementation).IsTrue();
        var handler = infos["Listener#m_ticker_Tick()."].Relationships.Single();
        await Assert.That(handler.Symbol).EndsWith(" Ticker#Tick().");
        await Assert.That(handler.IsReference).IsTrue();
        var refs = References(index, "Listener.cls", listener);
        await Assert.That(refs).Contains(r => r.StartsWith("m_ticker→", StringComparison.Ordinal) && r.EndsWith("Listener#m_ticker.", StringComparison.Ordinal));
        await Assert.That(References(index, "Ticker.cls", source)).Contains(r => r.StartsWith("Tick→", StringComparison.Ordinal));
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task ResolvesFormControlsFromOtherModules()
    {
        const string frm = """
            VERSION 5.00
            Begin VB.Form frmA
               Begin VB.TextBox txtName
               End
            End
            Attribute VB_Name = "frmA"
            Attribute VB_PredeclaredId = True
            """;
        const string bas = """
            Attribute VB_Name = "modB"
            Sub Fill()
                frmA.txtName.Text = "x"
            End Sub
            """;
        using var dir = TestSupport.Files(("frmA.frm", frm), ("modB.bas", bas));
        var index = TestSupport.Build(dir.Path);
        var refs = References(index, "modB.bas", bas);
        await Assert.That(refs.Count).IsEqualTo(2);
        await Assert.That(refs[1]).EndsWith("frmA#txtName.");
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task LabelsResolveForwardAndBackward()
    {
        const string bas = """
            Attribute VB_Name = "L"
            Sub S()
                On Error GoTo Fail
            Again:
                GoTo Again
            Fail:
                Resume Again
            End Sub
            """;
        using var dir = TestSupport.Files(("L.bas", bas));
        var index = TestSupport.Build(dir.Path);
        var refs = References(index, "L.bas", bas);
        await Assert.That(refs).IsEquivalentTo(["Fail→local 1", "Again→local 0", "Again→local 0"]);
    }

    [Test]
    public async Task CrossProjectReferencesUseTheOwningPackage()
    {
        const string lib = """
            Type=OleDll
            Class=cShared; cShared.cls
            Name="SharedLib"
            MajorVer=3
            """;
        const string cls = """
            VERSION 1.0 CLASS
            BEGIN
              MultiUse = -1  'True
            END
            Attribute VB_Name = "cShared"
            Attribute VB_Exposed = True
            Public Sub Run()
            End Sub
            """;
        const string app = """
            Type=Exe
            Reference=*\A..\Lib\SharedLib.vbp
            Module=modApp; modApp.bas
            Name="App"
            """;
        const string bas = """
            Attribute VB_Name = "modApp"
            Sub Main()
                Dim s As SharedLib.cShared
                Dim t As cShared
                s.Run
            End Sub
            """;
        const string group = "VBGROUP 5.0\nStartupProject=App\\App.vbp\nProject=Lib\\SharedLib.vbp";
        using var dir = TestSupport.Files(
            ("Lib/SharedLib.vbp", lib), ("Lib/cShared.cls", cls), ("App/App.vbp", app), ("App/modApp.bas", bas), ("All.vbg", group));
        var index = TestSupport.Build(dir.Path);
        var refs = References(index, "App/modApp.bas", bas);
        await Assert.That(refs).IsEquivalentTo([
            "SharedLib→SharedLib 3.0.0 SharedLib/", "cShared→SharedLib 3.0.0 cShared#", "cShared→SharedLib 3.0.0 cShared#",
            "s→local 0", "Run→SharedLib 3.0.0 cShared#Run().",
        ]);
        var vbpRefs = References(index, "App/App.vbp", app);
        await Assert.That(vbpRefs).Contains(r => r.EndsWith("SharedLib/", StringComparison.Ordinal));
        var groupRefs = References(index, "All.vbg", group);
        await Assert.That(groupRefs).IsEquivalentTo(["App\\App.vbp→App . App/", "Lib\\SharedLib.vbp→SharedLib 3.0.0 SharedLib/"]);
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task FilesSharedByTwoProjectsAreIndexedOnce()
    {
        const string p1 = "Type=Exe\nModule=modShared; modShared.bas\nName=\"One\"";
        const string p2 = "Type=Exe\nModule=modShared; modShared.bas\nName=\"Two\"";
        const string bas = "Attribute VB_Name = \"modShared\"\nPublic Sub X()\nEnd Sub";
        using var dir = TestSupport.Files(("One.vbp", p1), ("Two.vbp", p2), ("modShared.bas", bas));
        var index = TestSupport.Build(dir.Path);
        await Assert.That(index.Documents.Count(d => d.RelativePath == "modShared.bas")).IsEqualTo(1);
        var two = index.Documents.Single(d => d.RelativePath == "Two.vbp");
        await Assert.That(two.Occurrences.Any(o => o.Symbol.Contains(" One ", StringComparison.Ordinal) && o.Symbol.EndsWith("modShared#", StringComparison.Ordinal))).IsTrue();
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }

    [Test]
    public async Task ProjectNameClashesGetDistinctPackages()
    {
        const string p = "Type=Exe\nModule=m; m.bas\nName=\"Project1\"";
        const string bas = "Attribute VB_Name = \"m\"\nSub A()\nEnd Sub";
        using var dir = TestSupport.Files(("a/Project1.vbp", p), ("a/m.bas", bas), ("b/Project1.vbp", p), ("b/m.bas", bas));
        var index = TestSupport.Build(dir.Path);
        var modules = index.Documents.SelectMany(d => d.Symbols).Select(s => s.Symbol).Where(s => s.EndsWith(" m#", StringComparison.Ordinal)).ToList();
        await Assert.That(modules.Distinct().Count()).IsEqualTo(2);
        await Assert.That(TestSupport.Lint(index)).IsEmpty();
    }
}
