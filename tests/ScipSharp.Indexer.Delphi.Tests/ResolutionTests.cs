using ScipSharp.Indexer.Delphi.Project;

namespace ScipSharp.Indexer.Delphi.Tests;

/// <summary>Cross-unit resolution, forward declarations, implementations, overloads, generics, helpers, with and inherited.</summary>
public class ResolutionTests
{
    private const string Pkg = "scip-delphi delphi p 1 ";

    private static readonly DelphiIndexerOptions Options = new() { ProjectName = "p", ProjectVersion = "1" };

    private static (Index Index, Dictionary<string, string> Files) Build(Dictionary<string, string> files, DelphiIndexerOptions? options = null)
    {
        var index = TestUtil.Index(files, options ?? Options);
        return (index, files);
    }

    private static List<(string Symbol, int Roles)> At(Index index, Dictionary<string, string> files, string path, string text) =>
        TestUtil.OccurrencesOf(index, path, files[path], text);

    private static bool Has(List<(string Symbol, int Roles)> occs, string symbol, SymbolRole? role = null) =>
        occs.Any(o => o.Symbol == symbol && (role is null ? (o.Roles & (int)SymbolRole.Definition) == 0 : (o.Roles & (int)role) != 0));

    [Test]
    public async Task ResolvesAcrossUnitsAndQualifiedNames()
    {
        var (index, files) = Build(new()
        {
            ["Lib.Core.pas"] = """
                unit Lib.Core;
                interface
                type
                  TWidget = class
                  public
                    procedure Spin;
                  end;
                function MakeWidget: TWidget;
                implementation
                procedure TWidget.Spin; begin end;
                function MakeWidget: TWidget; begin Result := TWidget.Create; end;
                end.
                """,
            ["App.pas"] = """
                unit App;
                interface
                implementation
                uses Lib.Core;
                procedure Run;
                var W: TWidget;
                begin
                  W := Lib.Core.MakeWidget;
                  W.Spin;
                  MakeWidget.Spin;
                end;
                end.
                """,
        });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        await Assert.That(Has(At(index, files, "App.pas", "Lib.Core"), Pkg + "Lib/Core/")).IsTrue();
        await Assert.That(Has(At(index, files, "App.pas", "MakeWidget"), Pkg + "Lib/Core/MakeWidget().")).IsTrue();
        await Assert.That(Has(At(index, files, "App.pas", "Spin"), Pkg + "Lib/Core/TWidget#Spin().")).IsTrue();
        await Assert.That(Has(At(index, files, "App.pas", "TWidget"), Pkg + "Lib/Core/TWidget#")).IsTrue();
    }

    [Test]
    public async Task ForwardDeclarationsShareTheSymbol()
    {
        var (index, files) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                type
                  TNode = class;
                  TTree = class
                    Root: TNode;
                  end;
                  TNode = class
                    Next: TNode;
                  end;
                implementation
                end.
                """,
        });
        var occs = At(index, files, "U.pas", "TNode");
        await Assert.That(occs.Count(o => o.Symbol == Pkg + "U/TNode#")).IsEqualTo(4);
        await Assert.That(Has(occs, Pkg + "U/TNode#", SymbolRole.ForwardDefinition)).IsTrue();
        await Assert.That(Has(occs, Pkg + "U/TNode#", SymbolRole.Definition)).IsTrue();
        var doc = index.Documents.Single();
        await Assert.That(doc.Symbols.Count(s => s.Symbol == Pkg + "U/TNode#")).IsEqualTo(1);
        await Assert.That(Has(At(index, files, "U.pas", "Next"), Pkg + "U/TNode#Next.", SymbolRole.Definition)).IsTrue();
    }

    [Test]
    public async Task OverloadsGetDisambiguatorsAndImplementationsMatchBySignature()
    {
        var (index, files) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                type
                  TCalc = class
                    function Add(A, B: Integer): Integer; overload;
                    function Add(A, B: Double): Double; overload;
                  end;
                function Twice(X: Integer): Integer; overload;
                function Twice(const S: string): string; overload;
                implementation
                function TCalc.Add(A, B: Double): Double; begin Result := A + B; end;
                function TCalc.Add(A, B: Integer): Integer; begin Result := A + B; end;
                function Twice(const S: string): string; begin Result := S + S; end;
                function Twice(X: Integer): Integer; begin Result := X * 2; end;
                end.
                """,
        });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        var source = files["U.pas"].Split('\n');
        var doc = index.Documents.Single();
        string SymbolOnLine(int line, string text) => doc.Occurrences.Single(o =>
        {
            var r = o.GetSourceRange()!.Value;
            return r.Start.Line == line && source[line][r.Start.Character..r.End.Character] == text;
        }).Symbol;

        await Assert.That(SymbolOnLine(4, "Add")).IsEqualTo(Pkg + "U/TCalc#Add().");
        await Assert.That(SymbolOnLine(5, "Add")).IsEqualTo(Pkg + "U/TCalc#Add(+1).");
        await Assert.That(SymbolOnLine(10, "Add")).IsEqualTo(Pkg + "U/TCalc#Add(+1).");
        await Assert.That(SymbolOnLine(11, "Add")).IsEqualTo(Pkg + "U/TCalc#Add().");
        await Assert.That(SymbolOnLine(12, "Twice")).IsEqualTo(Pkg + "U/Twice(+1).");
        await Assert.That(SymbolOnLine(13, "Twice")).IsEqualTo(Pkg + "U/Twice().");
    }

    [Test]
    public async Task GenericSubstitutionFlowsThroughMembers()
    {
        var (index, files) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                uses System.Generics.Collections;
                type
                  TItem = class
                    Value: Integer;
                  end;
                  TBox<T> = class
                    Content: T;
                    function Get: T;
                  end;
                procedure Use;
                implementation
                function TBox<T>.Get: T; begin Result := Content; end;
                procedure Use;
                var
                  L: TList<TItem>;
                  D: TDictionary<string, TItem>;
                  B: TBox<TItem>;
                begin
                  L[0].Value := 1;
                  D['x'].Value := 2;
                  B.Get.Value := 3;
                  B.Content.Value := 4;
                  for var It in L do
                    It.Value := 5;
                end;
                end.
                """,
        });
        var occs = At(index, files, "U.pas", "Value");
        await Assert.That(occs.Count(o => o.Symbol == Pkg + "U/TItem#Value." && (o.Roles & (int)SymbolRole.Definition) == 0)).IsEqualTo(5);
        await Assert.That(occs.Any(o => (o.Roles & (int)SymbolRole.WriteAccess) != 0)).IsTrue();
    }

    [Test]
    public async Task HelpersWithAndInherited()
    {
        var (index, files) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                type
                  TBase = class
                    procedure Hello; virtual;
                    property Size: Integer read FSize;
                  private
                    FSize: Integer;
                  end;
                  TDerived = class(TBase)
                    procedure Hello; override;
                  end;
                  TBaseHelper = class helper for TBase
                    function Twice: Integer;
                  end;
                implementation
                procedure TBase.Hello; begin end;
                procedure TDerived.Hello;
                begin
                  inherited;
                  inherited Hello;
                  with Self do
                    WriteLn(Size, Twice);
                end;
                function TBaseHelper.Twice: Integer; begin Result := Size * 2; end;
                end.
                """,
        });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        await Assert.That(Has(At(index, files, "U.pas", "inherited"), Pkg + "U/TBase#Hello().")).IsTrue();
        await Assert.That(At(index, files, "U.pas", "Twice").Count(o => o.Symbol == Pkg + "U/TBaseHelper#Twice().")).IsEqualTo(3);
        await Assert.That(At(index, files, "U.pas", "Size").Count(o => o.Symbol == Pkg + "U/TBase#Size.")).IsEqualTo(3);
        var derivedHello = index.Documents.Single().Symbols.Single(s => s.Symbol == Pkg + "U/TDerived#Hello().");
        await Assert.That(derivedHello.Relationships.Single().Symbol).IsEqualTo(Pkg + "U/TBase#Hello().");
        await Assert.That(derivedHello.Relationships.Single().IsImplementation).IsTrue();
    }

    [Test]
    public async Task InterfaceImplementationsGetRelationships()
    {
        var (index, _) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                type
                  IGreeter = interface
                    procedure Greet;
                  end;
                  TGreeter = class(TInterfacedObject, IGreeter)
                    procedure Greet;
                  end;
                implementation
                procedure TGreeter.Greet; begin end;
                end.
                """,
        });
        var symbols = index.Documents.Single().Symbols;
        var cls = symbols.Single(s => s.Symbol == Pkg + "U/TGreeter#");
        await Assert.That(cls.Relationships.Select(r => r.Symbol).ToArray()).Contains(Pkg + "U/IGreeter#");
        var greet = symbols.Single(s => s.Symbol == Pkg + "U/TGreeter#Greet().");
        await Assert.That(greet.Relationships.Single().Symbol).IsEqualTo(Pkg + "U/IGreeter#Greet().");
        await Assert.That(cls.Kind).IsEqualTo(SymbolInformation.Types.Kind.Class);
        await Assert.That(symbols.Single(s => s.Symbol == Pkg + "U/IGreeter#").Kind).IsEqualTo(SymbolInformation.Types.Kind.Interface);
    }

    [Test]
    public async Task InterfaceRoutinesLinkToImplementationBodies()
    {
        var (index, files) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                /// <summary>Adds one.</summary>
                function Inc1(X: Integer): Integer;
                implementation
                function Helper: Integer; forward;
                function Inc1(X: Integer): Integer;
                begin
                  Result := X + Helper;
                end;
                function Helper: Integer; begin Result := 1; end;
                end.
                """,
        });
        var occs = At(index, files, "U.pas", "Inc1");
        await Assert.That(occs.Count).IsEqualTo(2);
        await Assert.That(occs.All(o => o.Symbol == Pkg + "U/Inc1().")).IsTrue();
        var info = index.Documents.Single().Symbols.Single(s => s.Symbol == Pkg + "U/Inc1().");
        await Assert.That(info.Documentation.Single()).IsEqualTo("Adds one.");
        await Assert.That(info.SignatureDocumentation.Text).IsEqualTo("function Inc1(X: Integer): Integer");
        await Assert.That(At(index, files, "U.pas", "Helper").All(o => o.Symbol == Pkg + "U/Helper().")).IsTrue();
        await Assert.That(At(index, files, "U.pas", "X").All(o => o.Symbol == Pkg + "U/Inc1().(X)")).IsTrue();
    }

    [Test]
    public async Task UsesSearchPathsNamespacesAndDprojDefines()
    {
        var dir = TestUtil.NewProject(new Dictionary<string, string>
        {
            ["app/App.dproj"] = """
                <Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
                  <PropertyGroup>
                    <MainSource>App.dpr</MainSource>
                    <DCC_UnitSearchPath>..\lib;$(DCC_UnitSearchPath)</DCC_UnitSearchPath>
                    <DCC_Namespace>Acme;$(DCC_Namespace)</DCC_Namespace>
                    <DCC_Define>FEATURE_X;$(DCC_Define)</DCC_Define>
                  </PropertyGroup>
                </Project>
                """,
            ["app/App.dpr"] = """
                program App;
                uses Tools;
                begin
                  {$IFDEF FEATURE_X}
                  Hammer;
                  {$ENDIF}
                end.
                """,
            ["lib/Acme.Tools.pas"] = """
                unit Acme.Tools;
                interface
                procedure Hammer;
                implementation
                procedure Hammer; begin end;
                end.
                """,
        });
        var index = TestUtil.Index(dir);
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        var dpr = index.Documents.Single(d => d.RelativePath == "app/App.dpr");
        await Assert.That(dpr.Occurrences.Any(o => o.Symbol.EndsWith(" Acme/Tools/Hammer().", StringComparison.Ordinal))).IsTrue();
        await Assert.That(dpr.Occurrences.Any(o => o.Symbol.EndsWith(" Acme/Tools/", StringComparison.Ordinal))).IsTrue();
        await Assert.That(dpr.Occurrences[0].Symbol).StartsWith("scip-delphi delphi App . ");
    }

    [Test]
    public async Task RtlUnitsResolveToStubSymbolsWithAndWithoutNamespaces()
    {
        var (index, files) = Build(new()
        {
            ["Old.pas"] = """
                unit Old;
                interface
                uses SysUtils, Classes;
                function F: TStringList;
                implementation
                function F: TStringList; begin Result := TStringList.Create; Result.Add(IntToStr(1)); end;
                end.
                """,
        });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        await Assert.That(Has(At(index, files, "Old.pas", "TStringList"), "scip-delphi delphi rtl . System/Classes/TStringList#")).IsTrue();
        await Assert.That(Has(At(index, files, "Old.pas", "Add"), "scip-delphi delphi rtl . System/Classes/TStringList#Add().")).IsTrue();
        await Assert.That(Has(At(index, files, "Old.pas", "SysUtils"), "scip-delphi delphi rtl . System/SysUtils/")).IsTrue();
        var ext = index.ExternalSymbols.Single(s => s.Symbol == "scip-delphi delphi rtl . System/SysUtils/IntToStr().");
        await Assert.That(ext.Kind).IsEqualTo(SymbolInformation.Types.Kind.Function);
        await Assert.That(ext.SignatureDocumentation.Text).StartsWith("function IntToStr(");
    }

    [Test]
    public async Task UnknownUnitsProduceExternalSymbols()
    {
        var (index, files) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                uses ThirdParty.Widgets;
                type
                  TMine = class(TFancyWidget)
                    procedure Go;
                  end;
                implementation
                procedure TMine.Go;
                begin
                  Paint;
                  ThirdParty.Widgets.GlobalThing := 1;
                end;
                end.
                """,
        });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        await Assert.That(Has(At(index, files, "U.pas", "TFancyWidget"), "scip-delphi delphi unknown . ThirdParty/Widgets/TFancyWidget#")).IsTrue();
        await Assert.That(Has(At(index, files, "U.pas", "Paint"), "scip-delphi delphi unknown . ThirdParty/Widgets/TFancyWidget#Paint.")).IsTrue();
        await Assert.That(Has(At(index, files, "U.pas", "GlobalThing"), "scip-delphi delphi unknown . ThirdParty/Widgets/GlobalThing.")).IsTrue();
    }

    [Test]
    public async Task ScopedEnumsAndAttributes()
    {
        var (index, files) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                type
                  {$SCOPEDENUMS ON}
                  TMode = (Fast, Slow);
                  {$SCOPEDENUMS OFF}
                  TFlag = (fA, fB);
                  TagAttribute = class(TCustomAttribute)
                    constructor Create(const N: string);
                  end;
                  [Tag('x')]
                  TThing = record
                    Mode: TMode;
                  end;
                const
                  M = TMode.Slow;
                  F = fB;
                implementation
                constructor TagAttribute.Create(const N: string); begin end;
                end.
                """,
        });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        await Assert.That(Has(At(index, files, "U.pas", "Slow"), Pkg + "U/TMode#Slow.")).IsTrue();
        await Assert.That(Has(At(index, files, "U.pas", "fB"), Pkg + "U/TFlag#fB.")).IsTrue();
        await Assert.That(Has(At(index, files, "U.pas", "Tag"), Pkg + "U/TagAttribute#")).IsTrue();
    }

    [Test]
    public async Task LocalsAndAnonymousMethodsUseLocalSymbols()
    {
        var (index, files) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                procedure P;
                implementation
                procedure P;
                var
                  Counter: Integer;
                  procedure Nested(Step: Integer);
                  begin
                    Inc(Counter, Step);
                  end;
                begin
                  Counter := 0;
                  Nested(2);
                  var F := function(A: Integer): Integer
                    begin
                      Result := A + Counter;
                    end;
                  Counter := F(1);
                end;
                end.
                """,
        });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        var counter = At(index, files, "U.pas", "Counter");
        await Assert.That(counter.Count).IsEqualTo(5);
        await Assert.That(counter.Select(o => o.Symbol).Distinct().Count()).IsEqualTo(1);
        await Assert.That(counter[0].Symbol).StartsWith("local ");
        await Assert.That(At(index, files, "U.pas", "Step").Select(o => o.Symbol).Distinct().Count()).IsEqualTo(1);
        await Assert.That(At(index, files, "U.pas", "A").Select(o => o.Symbol).Distinct().Count()).IsEqualTo(1);
    }

    [Test]
    public async Task IncludeFilesBecomeDocuments()
    {
        var (index, files) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                {$I decls.inc}
                implementation
                procedure FromInclude; begin end;
                end.
                """,
            ["decls.inc"] = "procedure FromInclude;\n",
        });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        await Assert.That(Has(At(index, files, "decls.inc", "FromInclude"), Pkg + "U/FromInclude().", SymbolRole.Definition)).IsTrue();
        await Assert.That(Has(At(index, files, "U.pas", "FromInclude"), Pkg + "U/FromInclude().")).IsTrue();
    }

    [Test]
    public async Task PackagesReferenceContainedUnits()
    {
        var (index, files) = Build(new()
        {
            ["Pkg.dpk"] = """
                package Pkg;
                requires rtl;
                contains A in 'A.pas';
                end.
                """,
            ["A.pas"] = "unit A; interface implementation end.",
        });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        await Assert.That(Has(At(index, files, "Pkg.dpk", "A"), Pkg + "A/")).IsTrue();
        await Assert.That(Has(At(index, files, "Pkg.dpk", "Pkg"), Pkg + "Pkg/", SymbolRole.Definition)).IsTrue();
        var info = index.Documents.Single(d => d.RelativePath == "Pkg.dpk").Symbols.Single(s => s.Symbol == Pkg + "Pkg/");
        await Assert.That(info.Kind).IsEqualTo(SymbolInformation.Types.Kind.Package);
    }

    [Test]
    public async Task SearchPathsOutsideTheRootResolveAsExternalLibraries()
    {
        var parent = TestUtil.NewProject(new Dictionary<string, string>
        {
            ["vendor/Lib.Strings.pas"] = """
                unit Lib.Strings;
                interface
                type
                  TStr = class
                    class function Shout(const S: string): string;
                  end;
                implementation
                class function TStr.Shout(const S: string): string; begin Result := S; end;
                end.
                """,
            ["app/Main.dpr"] = """
                program Main;
                uses Lib.Strings;
                begin
                  WriteLn(TStr.Shout('x'));
                end.
                """,
        });
        var root = Path.Combine(parent, "app");
        var index = TestUtil.Index(root, new DelphiIndexerOptions { SearchPaths = ["../vendor"] });
        await Assert.That(TestUtil.Lint(index)).IsEmpty();
        await Assert.That(index.Documents.Select(d => d.RelativePath).ToArray()).IsEquivalentTo(["Main.dpr"]);
        await Assert.That(index.ExternalSymbols.Any(s => s.Symbol == "scip-delphi delphi vendor . Lib/Strings/TStr#Shout().")).IsTrue();
    }

    [Test]
    public async Task EventPropertiesHaveEventKind()
    {
        var (index, _) = Build(new()
        {
            ["U.pas"] = """
                unit U;
                interface
                type
                  TEvt = procedure(Sender: TObject) of object;
                  TC = class
                  private
                    FOnFire: TEvt;
                  public
                    property OnFire: TEvt read FOnFire write FOnFire;
                  end;
                implementation
                end.
                """,
        });
        var info = index.Documents.Single().Symbols.Single(s => s.Symbol == Pkg + "U/TC#OnFire.");
        await Assert.That(info.Kind).IsEqualTo(SymbolInformation.Types.Kind.Event);
        await Assert.That(info.SignatureDocumentation.Text).IsEqualTo("property OnFire: TEvt read FOnFire write FOnFire");
    }
}
