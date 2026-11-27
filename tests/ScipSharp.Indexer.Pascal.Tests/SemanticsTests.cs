namespace ScipSharp.Indexer.Pascal.Tests;

/// <summary>Name resolution: nesting, forward declarations, with statements, records, units, overloads.</summary>
public class SemanticsTests
{
    private const string Pkg = "scip-pascal . ";

    [Test]
    public async Task NestedRoutinesFormDescriptorChains()
    {
        const string src = """
            program P;
            procedure Outer;
              procedure Middle;
                procedure Inner;
                begin
                end;
              begin
                Inner
              end;
            begin
              Middle
            end;
            begin
              Outer
            end.
            """;
        var (_, doc) = TestProject.Single("p.pas", src);
        var inner = doc.SymbolsAt(src, "Inner");
        await Assert.That(inner).Count().IsEqualTo(2);
        await Assert.That(inner[0]).EndsWith("P/Outer().Middle().Inner().");
        await Assert.That(inner[1]).IsEqualTo(inner[0]);
        await Assert.That(doc.SymbolsAt(src, "Middle")[1]).EndsWith("P/Outer().Middle().");
    }

    [Test]
    public async Task InnerScopesShadowOuterOnes()
    {
        const string src = """
            program P;
            var x: integer;
            procedure A;
            var x: real;
              procedure B;
              begin
                x := 1.5
              end;
            begin
              B
            end;
            begin
              x := 1
            end.
            """;
        var (_, doc) = TestProject.Single("p.pas", src);
        var xs = doc.SymbolsAt(src, "x");
        await Assert.That(xs[0]).EndsWith("P/x.");
        await Assert.That(xs[1]).StartsWith("local ");
        await Assert.That(xs[2]).IsEqualTo(xs[1]);
        await Assert.That(xs[3]).IsEqualTo(xs[0]);
    }

    [Test]
    public async Task ForwardDeclarationMergesWithBody()
    {
        const string src = """
            program P;
            procedure Later(n: integer); forward;
            procedure Sooner;
            begin
              Later(1)
            end;
            procedure Later;
            begin
              writeln(n)
            end;
            begin
            end.
            """;
        var (_, doc) = TestProject.Single("p.pas", src);
        var later = doc.Occurrences.Where(o => o.TextOf(src) == "Later").OrderBy(static o => o.Line()).ToList();
        await Assert.That(later).Count().IsEqualTo(3);
        await Assert.That(later.Select(static o => o.Symbol).Distinct().Count()).IsEqualTo(1);
        await Assert.That(later[0].HasRole(SymbolRole.ForwardDefinition)).IsTrue();
        await Assert.That(later[2].HasRole(SymbolRole.Definition)).IsTrue();
        var n = doc.SymbolsAt(src, "n");
        await Assert.That(n).Count().IsEqualTo(2);
        await Assert.That(n[1]).IsEqualTo(n[0]);
    }

    [Test]
    public async Task WithStatementResolvesFields()
    {
        const string src = """
            program P;
            type
              TPoint = record x, y: integer end;
              TRect = record topLeft, bottomRight: TPoint end;
            var
              r: TRect;
              x: integer;
            begin
              with r, topLeft do
                x := y;
              with r.bottomRight do
                y := x
            end.
            """;
        var (_, doc) = TestProject.Single("p.pas", src);
        var xs = doc.SymbolsAt(src, "x");
        await Assert.That(xs[0]).EndsWith("P/TPoint#x.");
        await Assert.That(xs[1]).EndsWith("P/x.");
        await Assert.That(xs[2]).EndsWith("P/TPoint#x.");
        await Assert.That(xs[3]).EndsWith("P/TPoint#x.");
        await Assert.That(doc.SymbolsAt(src, "topLeft")[1]).EndsWith("P/TRect#topLeft.");
    }

    [Test]
    public async Task VariantRecordFieldsBelongToTheRecord()
    {
        const string src = """
            program P;
            type
              TShape = (circle, square);
              TFigure = record
                name: char;
                case kind: TShape of
                  circle: (radius: real);
                  square: (side: real; filled: boolean)
              end;
            var f: TFigure;
            begin
              f.kind := circle;
              f.radius := 1;
              f.filled := true
            end.
            """;
        var (_, doc) = TestProject.Single("p.pas", src);
        foreach (var field in new[] { "kind", "radius", "side", "filled" })
        {
            var syms = doc.SymbolsAt(src, field);
            await Assert.That(syms[0]).EndsWith($"P/TFigure#{field}.");
        }

        await Assert.That(doc.SymbolsAt(src, "filled")[1]).EndsWith("P/TFigure#filled.");
        await Assert.That(doc.SymbolsAt(src, "circle")[1]).EndsWith("P/TShape#circle.");
    }

    [Test]
    public async Task UnitsResolveAcrossUsesAndCircularImplementationUses()
    {
        using var p = new TestProject()
            .Add("a.pas", """
                unit A;
                interface
                uses B;
                type TA = record b: TB end;
                procedure DoA;
                implementation
                procedure DoA;
                begin
                  DoB
                end;
                end.
                """)
            .Add("b.pas", """
                unit B;
                interface
                type TB = integer;
                procedure DoB;
                implementation
                uses A;
                procedure DoB;
                var x: TA;
                begin
                  x.b := 1;
                  A.DoA
                end;
                end.
                """);
        var index = p.Index();
        var a = index.Documents.Single(static d => d.RelativePath == "a.pas");
        var b = index.Documents.Single(static d => d.RelativePath == "b.pas");
        var dir = Path.GetFileName(p.Root);
        await Assert.That(a.Occurrences.Any(o => o.Symbol == $"{Pkg}{dir} . B/DoB().")).IsTrue();
        await Assert.That(b.Occurrences.Any(o => o.Symbol == $"{Pkg}{dir} . A/TA#")).IsTrue();
        await Assert.That(b.Occurrences.Any(o => o.Symbol == $"{Pkg}{dir} . A/TA#b.")).IsTrue();
        await Assert.That(b.Occurrences.Any(o => o.Symbol == $"{Pkg}{dir} . A/DoA().")).IsTrue();
        await Assert.That(a.Occurrences.Any(o => o.Symbol == $"{Pkg}{dir} . B/" && o.HasRole(SymbolRole.Import))).IsTrue();
    }

    [Test]
    public async Task OverloadsGetDisambiguatorsAndAreChosenByArguments()
    {
        const string src = """
            unit U;
            {$mode objfpc}
            interface
            function Max(a, b: integer): integer; overload;
            function Max(a, b: double): double; overload;
            function Max(a, b, c: integer): integer; overload;
            implementation
            function Max(a, b: integer): integer; begin Result := a end;
            function Max(a, b: double): double; begin Result := a end;
            function Max(a, b, c: integer): integer; begin Result := a end;
            procedure Test;
            begin
              Max(1, 2);
              Max(1.5, 2.5);
              Max(1, 2, 3)
            end;
            end.
            """;
        var (_, doc) = TestProject.Single("u.pas", src);
        var max = doc.SymbolsAt(src, "Max");
        await Assert.That(max).Count().IsEqualTo(9);
        await Assert.That(max[0]).EndsWith("U/Max().");
        await Assert.That(max[1]).EndsWith("U/Max(+1).");
        await Assert.That(max[2]).EndsWith("U/Max(+2).");
        await Assert.That(max[3..6]).IsEquivalentTo(max[0..3]);
        await Assert.That(max[6]).EndsWith("U/Max().");
        await Assert.That(max[7]).EndsWith("U/Max(+1).");
        await Assert.That(max[8]).EndsWith("U/Max(+2).");
    }

    [Test]
    public async Task ClassMembersResolveThroughAncestorsAndRelationshipsAreEmitted()
    {
        const string src = """
            unit U;
            {$mode objfpc}
            interface
            type
              IGreeter = interface
                procedure Greet;
              end;
              TBase = class(TInterfacedObject, IGreeter)
                FCount: integer;
                procedure Greet; virtual;
              end;
              TChild = class(TBase)
                procedure Greet; override;
              end;
            implementation
            procedure TBase.Greet; begin Inc(FCount) end;
            procedure TChild.Greet;
            begin
              inherited Greet;
              FCount := FCount + 1;
              inherited;
            end;
            end.
            """;
        var (index, doc) = TestProject.Single("u.pas", src);
        await Assert.That(doc.SymbolsAt(src, "FCount").Distinct().Single()).EndsWith("U/TBase#FCount.");
        var child = index.Info(doc.SymbolsAt(src, "TChild")[0])!;
        await Assert.That(child.Relationships.Any(static r => r.Symbol.EndsWith("U/TBase#", StringComparison.Ordinal) && r.IsImplementation)).IsTrue();
        var childGreet = index.Info(doc.SymbolsAt(src, "Greet")[2])!;
        await Assert.That(childGreet.Relationships.Any(static r => r.Symbol.EndsWith("U/TBase#Greet().", StringComparison.Ordinal))).IsTrue();
        var baseGreet = index.Info(doc.SymbolsAt(src, "Greet")[1])!;
        await Assert.That(baseGreet.Relationships.Any(static r => r.Symbol.EndsWith("U/IGreeter#Greet().", StringComparison.Ordinal))).IsTrue();
        await Assert.That(doc.SymbolsAt(src, "inherited").Single()).EndsWith("U/TBase#Greet().");
        await Assert.That(index.ExternalSymbols.Any(static s => s.Symbol == "scip-pascal fpc rtl . System/TInterfacedObject#")).IsTrue();
    }

    [Test]
    public async Task GenericsSubstituteTypeArguments()
    {
        const string src = """
            unit U;
            {$mode objfpc}
            interface
            type
              TItem = class
                Name: string;
              end;
              generic TBox<T> = class
                Value: T;
                function Get: T;
              end;
              TItemBox = specialize TBox<TItem>;
            var
              B: TItemBox;
              S: string;
            implementation
            function TBox.Get: T; begin Result := Value end;
            initialization
              S := B.Value.Name;
              S := B.Get.Name;
            end.
            """;
        var (_, doc) = TestProject.Single("u.pas", src);
        var names = doc.SymbolsAt(src, "Name");
        await Assert.That(names).Count().IsEqualTo(3);
        await Assert.That(names.Distinct().Single()).EndsWith("U/TItem#Name.");
        await Assert.That(doc.SymbolsAt(src, "T")[0]).EndsWith("U/TBox#[T]");
    }

    [Test]
    public async Task OperatorOverloadsAreReferencedFromExpressions()
    {
        const string src = """
            unit U;
            {$mode objfpc}
            interface
            type TV = record X: integer end;
            operator + (const A, B: TV) R: TV;
            implementation
            operator + (const A, B: TV) R: TV;
            begin
              R.X := A.X + B.X
            end;
            procedure T;
            var p, q: TV;
            begin
              p := p + q
            end;
            end.
            """;
        var (_, doc) = TestProject.Single("u.pas", src);
        var plus = doc.SymbolsAt(src, "+");
        await Assert.That(plus.Distinct().Single()).EndsWith("U/+().");
        await Assert.That(plus).Count().IsEqualTo(3);
    }

    [Test]
    public async Task UnresolvedRtlNamesBecomeExternalSymbols()
    {
        const string src = """
            program P;
            uses SysUtils, Classes;
            var L: TStringList;
            begin
              L := TStringList.Create;
              L.Add(IntToStr(42));
              WriteLn(L.Count);
              L.Free
            end.
            """;
        var (index, doc) = TestProject.Single("p.pas", src);
        await Assert.That(doc.SymbolsAt(src, "IntToStr").Single()).IsEqualTo("scip-pascal fpc rtl . SysUtils/IntToStr().");
        await Assert.That(doc.SymbolsAt(src, "Add").Single()).IsEqualTo("scip-pascal fpc rtl . Classes/TStrings#Add().");
        await Assert.That(doc.SymbolsAt(src, "Free").Single()).IsEqualTo("scip-pascal fpc rtl . System/TObject#Free().");
        await Assert.That(doc.SymbolsAt(src, "SysUtils").Single()).IsEqualTo("scip-pascal fpc rtl . SysUtils/");
        await Assert.That(index.ExternalSymbols.Select(static s => s.Symbol)).Contains("scip-pascal fpc rtl . Classes/TStringList#");
    }

    [Test]
    public async Task NamesOfAnUnknownUnitAreInferred()
    {
        const string src = """
            program P;
            uses ThirdParty;
            var W: TWidget;
            begin
              W := TWidget.Create;
              W.Paint(1);
              Configure
            end.
            """;
        var (_, doc) = TestProject.Single("p.pas", src);
        await Assert.That(doc.SymbolsAt(src, "TWidget")[0]).IsEqualTo("scip-pascal . . . ThirdParty/TWidget#");
        await Assert.That(doc.SymbolsAt(src, "Paint").Single()).IsEqualTo("scip-pascal . . . ThirdParty/TWidget#Paint().");
        await Assert.That(doc.SymbolsAt(src, "Configure").Single()).StartsWith("scip-pascal . . . ThirdParty/Configure");
    }

    [Test]
    public async Task LabelsAndGotoAreLocal()
    {
        const string src = """
            program P;
            label 10, done;
            begin
              goto 10;
            10: goto done;
            done:
            end.
            """;
        var (_, doc) = TestProject.Single("p.pas", src);
        var ten = doc.SymbolsAt(src, "10");
        await Assert.That(ten).Count().IsEqualTo(3);
        await Assert.That(ten.Distinct().Single()).StartsWith("local ");
        await Assert.That(doc.SymbolsAt(src, "done").Distinct().Single()).StartsWith("local ");
    }

    [Test]
    public async Task ClassicIdentifiersThatLaterBecameKeywordsAreDemoted()
    {
        const string src = """
            program P;
            type operator = (plus, minus);
            var string: integer; object: operator;
            begin
              string := 1;
              object := plus
            end.
            """;
        var (_, doc) = TestProject.Single("p.pas", src);
        await Assert.That(doc.SymbolsAt(src, "operator")).Count().IsEqualTo(2);
        await Assert.That(doc.SymbolsAt(src, "string")).Count().IsEqualTo(2);
        await Assert.That(doc.SymbolsAt(src, "object")[1]).EndsWith("P/object.");
    }

    [Test]
    public async Task TurboObjectsAndExtendedNew()
    {
        const string src = """
            program P;
            type
              PNode = ^TNode;
              TNode = object
                Value: integer;
                constructor Init(V: integer);
                destructor Done; virtual;
              end;
            constructor TNode.Init(V: integer); begin Value := V end;
            destructor TNode.Done; begin end;
            var N: PNode;
            begin
              New(N, Init(3));
              N^.Value := 4;
              Dispose(N, Done)
            end.
            """;
        var (_, doc) = TestProject.Single("p.pas", src);
        await Assert.That(doc.SymbolsAt(src, "Init").Distinct().Single()).EndsWith("P/TNode#Init().");
        await Assert.That(doc.SymbolsAt(src, "Done").Distinct().Single()).EndsWith("P/TNode#Done().");
        await Assert.That(doc.SymbolsAt(src, "Value").Distinct().Single()).EndsWith("P/TNode#Value.");
    }

    [Test]
    public async Task PropertiesAndDefaultIndexers()
    {
        const string src = """
            unit U;
            {$mode objfpc}
            interface
            type
              TItem = class Caption: string; end;
              TList = class
              private
                function GetItem(I: integer): TItem;
              public
                property Items[I: integer]: TItem read GetItem; default;
              end;
            implementation
            function TList.GetItem(I: integer): TItem; begin Result := nil end;
            procedure Use(L: TList);
            begin
              L[0].Caption := 'x';
              L.Items[1].Caption := 'y'
            end;
            end.
            """;
        var (_, doc) = TestProject.Single("u.pas", src);
        await Assert.That(doc.SymbolsAt(src, "Caption").Distinct().Single()).EndsWith("U/TItem#Caption.");
        await Assert.That(doc.SymbolsAt(src, "GetItem")[1]).EndsWith("U/TList#GetItem().");
        await Assert.That(doc.SymbolsAt(src, "Items")[1]).EndsWith("U/TList#Items.");
    }

    [Test]
    public async Task HelpersExtendTheirTarget()
    {
        const string src = """
            unit U;
            {$mode objfpc}{$modeswitch typehelpers}
            interface
            type
              TFoo = class Size: integer; end;
              TFooHelper = class helper for TFoo
                function Twice: integer;
              end;
            implementation
            function TFooHelper.Twice: integer; begin Result := Size * 2 end;
            procedure Use(F: TFoo); begin F.Twice end;
            end.
            """;
        var (_, doc) = TestProject.Single("u.pas", src);
        await Assert.That(doc.SymbolsAt(src, "Size")[1]).EndsWith("U/TFoo#Size.");
        await Assert.That(doc.SymbolsAt(src, "Twice")[2]).EndsWith("U/TFooHelper#Twice().");
    }

    [Test]
    public async Task DocumentationAndSignaturesAreAttached()
    {
        const string src = """
            unit U;
            interface
            type
              { A point in the plane }
              TPoint = record
                X: integer; // horizontal
                Y: integer; // vertical
              end;
            // Returns the origin.
            // Always (0, 0).
            function Origin: TPoint;
            implementation
            function Origin: TPoint; begin end;
            end.
            """;
        var (index, doc) = TestProject.Single("u.pas", src);
        var point = index.Info(doc.SymbolsAt(src, "TPoint")[0])!;
        await Assert.That(point.Documentation.Single()).IsEqualTo("A point in the plane");
        await Assert.That(point.Kind).IsEqualTo(SymbolInformation.Types.Kind.Struct);
        var x = index.Info(doc.SymbolsAt(src, "X")[0])!;
        await Assert.That(x.Documentation.Single()).IsEqualTo("horizontal");
        await Assert.That(x.SignatureDocumentation.Text).IsEqualTo("X: integer");
        var origin = index.Info(doc.SymbolsAt(src, "Origin")[0])!;
        await Assert.That(origin.Documentation.Single()).IsEqualTo("Returns the origin.\nAlways (0, 0).");
        await Assert.That(origin.SignatureDocumentation.Text).IsEqualTo("function Origin: TPoint");
        await Assert.That(origin.Kind).IsEqualTo(SymbolInformation.Types.Kind.Function);
    }
}
