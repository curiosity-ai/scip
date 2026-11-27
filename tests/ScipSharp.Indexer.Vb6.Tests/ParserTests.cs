using ScipSharp.Indexer.Vb6.Project;
using ScipSharp.Indexer.Vb6.Syntax;
using ScipSharp.Indexer.Vb6.Text;

namespace ScipSharp.Indexer.Vb6.Tests;

public class ParserTests
{
    private static ModuleSyntax Parse(string text)
    {
        var source = SourceText.FromString(text.ReplaceLineEndings("\r\n"));
        return ModuleParser.Parse(Lexer.Lex(source.Lines).Tokens, source);
    }

    [Test]
    public async Task ParsesDeclarations()
    {
        var m = Parse("""
            Attribute VB_Name = "modX"
            Option Explicit
            Public Declare PtrSafe Function GetTickCount Lib "kernel32" Alias "GetTickCount" () As Long
            Private Type T
                a As Long
                b(1 To 3) As String * 10
            End Type
            Enum E
                One = 1
                Two
            End Enum
            Public Const A = 1, B As Long = 2
            Dim x As Integer, y
            Public WithEvents w As cFoo
            Event Changed(ByVal v As Long)
            Friend Static Function F(Optional ByRef p As Variant = Empty, ParamArray rest() As Variant) As Boolean
                F = True
            End Function
            Property Get P() As Long
            End Property
            Property Let P(ByVal v As Long)
            End Property
            """);
        await Assert.That(m.NameAttribute!.Text).IsEqualTo("modX");
        await Assert.That(m.OptionExplicit).IsTrue();
        var names = m.Members.Select(x => $"{x.GetType().Name}:{x.Name.Text}").ToArray();
        await Assert.That(names).IsEquivalentTo(new[]
        {
            "DeclareSyntax:GetTickCount", "UdtSyntax:T", "EnumSyntax:E", "VariableSyntax:A", "VariableSyntax:B", "VariableSyntax:x",
            "VariableSyntax:y", "VariableSyntax:w", "EventSyntax:Changed", "ProcedureSyntax:F", "ProcedureSyntax:P", "ProcedureSyntax:P",
        });
        var decl = (DeclareSyntax)m.Members[0];
        await Assert.That(decl.Library).IsEqualTo("kernel32");
        await Assert.That(decl.Alias).IsEqualTo("GetTickCount");
        await Assert.That(decl.ReturnType!.Text).IsEqualTo("Long");
        var udt = (UdtSyntax)m.Members[1];
        await Assert.That(udt.Fields.Select(f => f.Name.Text).ToArray()).IsEquivalentTo(["a", "b"]);
        await Assert.That(udt.Fields[1].IsArray).IsTrue();
        await Assert.That(udt.Fields[1].Type!.FixedLength.Single().Text).IsEqualTo("10");
        await Assert.That(((EnumSyntax)m.Members[2]).Members.Count).IsEqualTo(2);
        var b = (VariableSyntax)m.Members[4];
        await Assert.That(b.IsConst).IsTrue();
        await Assert.That(b.Signature).IsEqualTo("Public Const B As Long = 2");
        await Assert.That(((VariableSyntax)m.Members[7]).IsWithEvents).IsTrue();
        var f = (ProcedureSyntax)m.Members[9];
        await Assert.That(f.Visibility).IsEqualTo(Visibility.Friend);
        await Assert.That(f.IsStatic).IsTrue();
        await Assert.That(f.Kind).IsEqualTo(ProcedureKind.Function);
        await Assert.That(f.Parameters.Select(p => (p.Name.Text, p.IsOptional, p.IsParamArray)).ToArray())
            .IsEquivalentTo([("p", true, false), ("rest", false, true)]);
        await Assert.That(f.Body.Count).IsEqualTo(1);
        await Assert.That(f.Closed).IsTrue();
        await Assert.That(((ProcedureSyntax)m.Members[11]).Kind).IsEqualTo(ProcedureKind.PropertyLet);
    }

    [Test]
    public async Task RecoversFromMissingEndSub()
    {
        var m = Parse("""
            Sub A()
                x = 1
            Sub B()
            End Sub
            """);
        var procs = m.Procedures.ToList();
        await Assert.That(procs.Select(p => p.Name.Text).ToArray()).IsEquivalentTo(["A", "B"]);
        await Assert.That(procs[0].Closed).IsFalse();
        await Assert.That(procs[1].Closed).IsTrue();
    }

    [Test]
    public async Task ToleratesGarbage()
    {
        var m = Parse("""
            Public Sub (
            Private Declare Function
            Type
            Public Enum
            Dim ,,, As
            ))) = "
            Sub Ok()
            End Sub
            """);
        await Assert.That(m.Procedures.Single().Name.Text).IsEqualTo("Ok");
    }

    [Test]
    public async Task SplitsStatementsAndLabels()
    {
        var tokens = Lexer.Lex(SourceText.FromString("EH: x = 1: y = 2\r\n10 Print x\r\nFoo").Lines).Tokens;
        var statements = ModuleParser.SplitStatements(tokens);
        await Assert.That(statements.Select(s => s.IsLabel).ToArray()).IsEquivalentTo([true, false, false, false, false]);
    }

    [Test]
    public async Task EvaluatesConditionalCompilation()
    {
        var lines = SourceText.FromString("""
            #Const DEBUG_MODE = 1
            #If DEBUG_MODE And Not Win64 Then
            a
            #ElseIf VBA7 Then
            b
            #Else
            c
            #End If
            #If FOO = "x" Then
            d
            #End If
            e
            """.ReplaceLineEndings("\n")).Lines;
        var active = Preprocessor.Evaluate(lines, 0, Preprocessor.DefaultConstants);
        var activeText = lines.Where((_, i) => active[i]).ToArray();
        await Assert.That(activeText).IsEquivalentTo(["a", "e"]);

        var consts = new Dictionary<string, object>(Preprocessor.DefaultConstants, StringComparer.OrdinalIgnoreCase) { ["FOO"] = "x", ["Win64"] = -1L };
        active = Preprocessor.Evaluate(lines, 0, consts);
        await Assert.That(lines.Where((_, i) => active[i]).ToArray()).IsEquivalentTo(["c", "d", "e"]);
    }

    [Test]
    public async Task ParsesCondCompDefinitions()
    {
        var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        Preprocessor.ParseDefinitions("A = 1 : B = &H10 : C = -1 : D", d);
        await Assert.That(d["A"]).IsEqualTo(1L);
        await Assert.That(d["B"]).IsEqualTo(16L);
        await Assert.That(d["C"]).IsEqualTo(-1L);
        await Assert.That(d["D"]).IsEqualTo(-1L);
    }

    [Test]
    public async Task ParsesDesignerBlock()
    {
        var lines = SourceText.FromString("""
            VERSION 5.00
            Object = "{831FDD16-0C5C-11D2-A9FC-0000F8754DA1}#2.0#0"; "MSCOMCTL.OCX"
            Begin VB.Form Form1
               Caption         =   "Hello"
               BeginProperty Font
                  Name = "Tahoma"
               EndProperty
               Begin VB.PictureBox Picture1
                  Begin VB.CommandButton Command1
                     Index           =   0
                  End
                  Begin VB.CommandButton Command1
                     Index           =   1
                  End
               End
               Begin MSComctlLib.ListView lv
               End
            End
            Attribute VB_Name = "Form1"
            """.ReplaceLineEndings("\r\n")).Lines;
        var header = DesignerParser.Parse(lines);
        await Assert.That(header.CodeStartLine).IsEqualTo(18);
        await Assert.That(header.Root!.Name).IsEqualTo("Form1");
        await Assert.That(header.Root.Caption).IsEqualTo("Hello");
        await Assert.That(header.Controls.Select(c => $"{c.QualifiedType} {c.Name} {c.Index}").ToArray())
            .IsEquivalentTo(["VB.PictureBox Picture1 ", "VB.CommandButton Command1 0", "VB.CommandButton Command1 1", "MSComctlLib.ListView lv "]);
        var lv = header.Controls.Last();
        await Assert.That(lv.NameSpan).IsEqualTo(new Span(15, 30, 32));
        await Assert.That(lv.TypeSpan).IsEqualTo(new Span(15, 21, 29));
        await Assert.That(header.ObjectReferences.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ParsesClassHeaderAndPlainModules()
    {
        var cls = DesignerParser.Parse(SourceText.FromString("VERSION 1.0 CLASS\r\nBEGIN\r\n  MultiUse = -1  'True\r\nEND\r\nAttribute VB_Name = \"C\"").Lines);
        await Assert.That(cls.CodeStartLine).IsEqualTo(4);
        await Assert.That(cls.Root).IsNull();
        var bas = DesignerParser.Parse(SourceText.FromString("Attribute VB_Name = \"M\"").Lines);
        await Assert.That(bas.CodeStartLine).IsEqualTo(0);
    }

    [Test]
    public async Task ParsesVbp()
    {
        var lines = SourceText.FromString("""
            Type=OleDll
            Reference=*\G{00020430-0000-0000-C000-000000000046}#2.0#0#C:\Windows\SysWOW64\stdole2.tlb#OLE Automation
            Reference=*\A..\Other\Other.vbp
            Object={F9043C88-F6F2-101A-A3C9-08002B2F49FB}#1.2#0; COMDLG32.OCX
            Module=modMain; src\modMain.bas
            Class=cFoo;  cFoo.cls
            Form=frmMain.frm
            UserControl=ctl\ucX.ctl
            Startup="Sub Main"
            Name="MyLib"
            CondComp="DEBUG = 1"
            MajorVer=2
            MinorVer=5
            RevisionVer=17
            [MS Transaction Server]
            Name="Ignored"
            """.ReplaceLineEndings("\r\n")).Lines;
        var vbp = VbpFile.Parse("x.vbp", lines);
        await Assert.That(vbp.Name).IsEqualTo("MyLib");
        await Assert.That(vbp.NameSpan).IsEqualTo(new Span(9, 6, 11));
        await Assert.That(vbp.Version).IsEqualTo("2.5.17");
        await Assert.That(vbp.Type).IsEqualTo("OleDll");
        await Assert.That(vbp.CondComp).IsEqualTo("DEBUG = 1");
        await Assert.That(vbp.Startup).IsEqualTo("Sub Main");
        await Assert.That(vbp.Items.Select(i => $"{i.Kind}:{i.Name}:{i.RawPath}").ToArray())
            .IsEquivalentTo(["Module:modMain:src\\modMain.bas", "Class:cFoo:cFoo.cls", "Form::frmMain.frm", "UserControl::ctl\\ucX.ctl"]);
        await Assert.That(vbp.Items[1].NameSpan).IsEqualTo(new Span(5, 6, 10));
        await Assert.That(vbp.Items[1].PathSpan).IsEqualTo(new Span(5, 13, 21));
        await Assert.That(vbp.References.Select(r => r.ProjectPath).ToArray()).IsEquivalentTo([null, "..\\Other\\Other.vbp"]);
        await Assert.That(vbp.References[0].Description).IsEqualTo("OLE Automation");
    }

    [Test]
    public async Task ParsesVbg()
    {
        var g = VbgFile.Parse("g.vbg", SourceText.FromString("VBGROUP 5.0\r\nStartupProject=App\\App.vbp\r\nProject=Lib\\Lib.vbp").Lines);
        await Assert.That(g.Projects.Select(p => p.RawPath).ToArray()).IsEquivalentTo(["App\\App.vbp", "Lib\\Lib.vbp"]);
    }
}
