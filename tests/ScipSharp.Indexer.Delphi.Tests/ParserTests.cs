using ScipSharp.Indexer.Delphi.Syntax;

namespace ScipSharp.Indexer.Delphi.Tests;

public class ParserTests
{
    private static string Name(FileSyntax f, int tok) => f.Tokens[tok].Text;

    [Test]
    public async Task ParsesUnitSections()
    {
        var f = TestUtil.Parse("""
            unit A.B;
            interface
            uses System.SysUtils, Foo in 'Foo.pas';
            type TX = class end;
            implementation
            uses Bar;
            procedure P; begin end;
            initialization
              P;
            finalization
            end.
            """);
        await Assert.That(f.Kind).IsEqualTo(FileKind.Unit);
        await Assert.That(f.Name).IsEqualTo("A.B");
        await Assert.That(f.InterfaceUses.Select(u => u.Name).ToArray()).IsEquivalentTo(["System.SysUtils", "Foo"]);
        await Assert.That(f.InterfaceUses[1].InPath).IsEqualTo("Foo.pas");
        await Assert.That(f.ImplementationUses.Single().Name).IsEqualTo("Bar");
        await Assert.That(f.InterfaceDecls.Count).IsEqualTo(1);
        await Assert.That(f.ImplementationDecls.OfType<RoutineDecl>().Single().Body).IsNotNull();
        await Assert.That(f.Blocks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ParsesInterfaceWithGuidAndFollowingTypes()
    {
        var f = TestUtil.Parse("""
            unit U;
            interface
            type
              IDrawable = interface
                ['{6A1F5B2C-3D4E-4F60-8A9B-0C1D2E3F4A5B}']
                procedure Draw;
                function GetName: string;
                property Name: string read GetName;
              end;
              TNext = class end;
            implementation
            end.
            """);
        var types = f.InterfaceDecls.OfType<TypeDecl>().ToList();
        await Assert.That(types.Select(t => Name(f, t.NameTok)).ToArray()).IsEquivalentTo(["IDrawable", "TNext"]);
        var s = (StructTypeNode)types[0].Type!;
        await Assert.That(s.Kind).IsEqualTo(StructKind.Interface);
        await Assert.That(s.GuidTok).IsNotNull();
        await Assert.That(s.Members.Count).IsEqualTo(3);
    }

    [Test]
    public async Task ParsesClassMembersVisibilityAndNestedTypes()
    {
        var f = TestUtil.Parse("""
            unit U;
            interface
            type
              [Serializable]
              TShape = class abstract(TInterfacedObject, IDrawable)
              private
                FName: string;
                procedure SetName(const Value: string);
              strict protected
                function GetName: string; virtual;
              public
                type
                  TPoint = record X, Y: Integer; end;
                const
                  DefaultName = 'shape';
                class var Count: Integer;
                constructor Create(const AName: string); virtual;
                procedure Move(DX, DY: Integer); overload;
                class function Describe(Shape: TShape): string; static;
                property Name: string read GetName write SetName;
                property Items[Index: Integer]: string read GetName; default;
              end;
            implementation
            end.
            """);
        var td = f.InterfaceDecls.OfType<TypeDecl>().Single();
        await Assert.That(td.Attributes.Count).IsEqualTo(1);
        var s = (StructTypeNode)td.Type!;
        await Assert.That(s.IsAbstract).IsTrue();
        await Assert.That(s.Ancestors.Count).IsEqualTo(2);
        await Assert.That(s.Members.OfType<TypeDecl>().Single().Visibility).IsEqualTo(Visibility.Public);
        await Assert.That(s.Members.OfType<ConstDecl>().Count()).IsEqualTo(1);
        await Assert.That(s.Members.OfType<VarDecl>().Single(v => v.Kind == VarKind.ClassVar).NameToks.Count).IsEqualTo(1);
        var getName = s.Members.OfType<RoutineDecl>().Single(r => Name(f, r.NameTok) == "GetName");
        await Assert.That(getName.Visibility).IsEqualTo(Visibility.StrictProtected);
        await Assert.That(getName.Directives.Contains("virtual")).IsTrue();
        var describe = s.Members.OfType<RoutineDecl>().Single(r => Name(f, r.NameTok) == "Describe");
        await Assert.That(describe.IsClassMethod).IsTrue();
        var props = s.Members.OfType<PropertyDecl>().ToList();
        await Assert.That(props[0].Specs.Select(p => p.Keyword).ToArray()).IsEquivalentTo(["read", "write"]);
        await Assert.That(props[1].IsDefault).IsTrue();
        await Assert.That(props[1].IndexParams.Single().NameToks.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ParsesGenericsHelpersAndForwardDeclarations()
    {
        var f = TestUtil.Parse("""
            unit U;
            interface
            type
              TFoo = class;
              TList<T: class, constructor> = class(TObjectList<T>)
                function First<U>: U;
              end;
              TFoo = class(TBase);
              TStrHelper = record helper for string
                function Upper: string;
              end;
              TProc = reference to procedure(A: Integer);
              TEvt = procedure(Sender: TObject) of object;
              TArr = array[0..9] of array of Integer;
              TRange = Low(Integer)..High(Integer);
              PFoo = ^TFoo;
              TFooClass = class of TFoo;
            implementation
            function TList<T>.First<U>: U; begin end;
            end.
            """);
        var types = f.InterfaceDecls.OfType<TypeDecl>().ToList();
        await Assert.That(((StructTypeNode)types[0].Type!).IsForward).IsTrue();
        await Assert.That(types[1].TypeParams.Count).IsEqualTo(1);
        await Assert.That(((StructTypeNode)types[2].Type!).IsForward).IsFalse();
        await Assert.That(((StructTypeNode)types[3].Type!).Kind).IsEqualTo(StructKind.RecordHelper);
        await Assert.That(((ProcTypeNode)types[4].Type!).IsReference).IsTrue();
        await Assert.That(((ProcTypeNode)types[5].Type!).OfObject).IsTrue();
        await Assert.That(types[6].Type).IsTypeOf<ArrayTypeNode>();
        await Assert.That(types[7].Type).IsTypeOf<ExprTypeNode>();
        await Assert.That(types[8].Type).IsTypeOf<PointerTypeNode>();
        await Assert.That(types[9].Type).IsTypeOf<ClassOfTypeNode>();
        var impl = f.ImplementationDecls.OfType<RoutineDecl>().Single();
        await Assert.That(impl.NameParts.Count).IsEqualTo(2);
        await Assert.That(impl.TypeParams.Count).IsEqualTo(1);
        await Assert.That(impl.NameParts[0].Args!.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ParsesVariantRecordsAndMethodResolution()
    {
        var f = TestUtil.Parse("""
            unit U;
            interface
            type
              TVar = record
                Kind: Integer;
                case Tag: Byte of
                  0: (I: Integer);
                  1, 2: (S: Single; D: Double);
              end;
              TSink = class(TInterfacedObject, ISink)
                procedure ISink.Write = WriteLine;
                procedure WriteLine;
              end;
            implementation
            end.
            """);
        var rec = (StructTypeNode)f.InterfaceDecls.OfType<TypeDecl>().First().Type!;
        var fields = rec.Members.OfType<VarDecl>().SelectMany(v => v.NameToks).Select(t => Name(f, t)).ToArray();
        await Assert.That(fields).IsEquivalentTo(["Kind", "Tag", "I", "S", "D"]);
        var sink = (StructTypeNode)f.InterfaceDecls.OfType<TypeDecl>().Last().Type!;
        var res = sink.Members.OfType<RoutineDecl>().First();
        await Assert.That(res.ResolutionTarget).IsNotNull();
        await Assert.That(Name(f, res.ResolutionTarget!.Value)).IsEqualTo("WriteLine");
    }

    [Test]
    public async Task ParsesProgramsAndPackages()
    {
        var program = TestUtil.Parse("""
            program P;
            uses A in 'A.pas' {Form1};
            var X: Integer;
            begin
              X := 1;
            end.
            """);
        await Assert.That(program.Kind).IsEqualTo(FileKind.Program);
        await Assert.That(program.InterfaceUses.Single().InPath).IsEqualTo("A.pas");
        await Assert.That(program.Blocks.Count).IsEqualTo(1);
        var package = TestUtil.Parse("""
            package Pkg;
            requires rtl, vcl;
            contains A in 'A.pas', B in 'sub\B.pas';
            end.
            """);
        await Assert.That(package.Kind).IsEqualTo(FileKind.Package);
        await Assert.That(package.Requires.Select(r => r.Name).ToArray()).IsEquivalentTo(["rtl", "vcl"]);
        await Assert.That(package.InterfaceUses.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RecoversFromGarbage()
    {
        var f = TestUtil.Parse("""
            unit U;
            interface
            type
              TA = class
                procedure X(;
              end;
              ))) ;;
              TB = record A: Integer; end;
            implementation
            procedure Q begin end;
            end.
            """);
        await Assert.That(f.InterfaceDecls.OfType<TypeDecl>().Any(t => Name(f, t.NameTok) == "TB")).IsTrue();
    }
}
