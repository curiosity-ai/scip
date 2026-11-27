namespace ScipSharp.Indexer.Fortran.Tests;

/// <summary>Name resolution: modules across files, use association, host association, generics, types, implicit typing.</summary>
public class ResolutionTests
{
    private const string Pkg = "scip-fortran . proj . ";

    private static FortranOptions Opts => new() { PackageName = "proj" };

    [Test]
    public async Task ModulesResolveAcrossFilesInAnyOrder()
    {
        // a.f90 (the user) sorts before z.f90 (the module): pass 1 collects every module first.
        var p = TestSupport.Build(new()
        {
            ["a.f90"] = "program p\n  use zmod, only: answer, tell => report\n  print *, answer\n  call tell()\nend program\n",
            ["z.f90"] = "module zmod\n  integer :: answer = 42\ncontains\n  subroutine report()\n  end subroutine\nend module\n",
        }, Opts);
        await Assert.That(p.Sym("a.f90", "use zmod", "zmod")).IsEqualTo(Pkg + "zmod/");
        await Assert.That(p.Sym("a.f90", "print", "answer")).IsEqualTo(Pkg + "zmod/answer.");
        await Assert.That(p.Sym("a.f90", "call tell", "tell")).IsEqualTo(Pkg + "zmod/report().");
        await Assert.That(p.Sym("a.f90", "use zmod", "report")).IsEqualTo(Pkg + "zmod/report().");
        var import = p.At("a.f90", "use zmod", "answer").Single();
        await Assert.That(import.SymbolRoles & (int)SymbolRole.Import).IsEqualTo((int)SymbolRole.Import);
        await Assert.That(TestSupport.Lint(p.Index)).IsEmpty();
    }

    [Test]
    public async Task PrivateEntitiesAreNotExportedButReExportsAre()
    {
        var p = TestSupport.Build(new()
        {
            ["m.f90"] = """
                module base
                  implicit none
                  private
                  integer, public :: shared = 1
                  integer :: hidden = 2
                end module
                module facade
                  use base
                  implicit none
                end module
                program p
                  use facade
                  implicit none
                  print *, shared, hidden
                end program
                """,
        }, Opts);
        await Assert.That(p.Sym("m.f90", "print", "shared")).IsEqualTo(Pkg + "base/shared.");
        await Assert.That(p.Sym("m.f90", "print", "hidden")).IsNull();
    }

    [Test]
    public async Task HostAssociationAndInternalProcedures()
    {
        var p = TestSupport.Build("h.f90", """
            module m
              implicit none
              real :: g
            contains
              subroutine outer(x)
                real :: x, y
                y = x
                call inner()
              contains
                subroutine inner()
                  y = y + g + x
                end subroutine inner
              end subroutine outer
            end module m
            """, Opts);
        await Assert.That(p.Sym("h.f90", "call inner", "inner")).IsEqualTo(Pkg + "m/outer().inner().");
        var yOuter = p.Sym("h.f90", "y = x", "y");
        await Assert.That(yOuter).StartsWith("local ");
        await Assert.That(p.Sym("h.f90", "y = y + g", "y")).IsEqualTo(yOuter);
        await Assert.That(p.Sym("h.f90", "y = y + g", "g")).IsEqualTo(Pkg + "m/g.");
        await Assert.That(p.Sym("h.f90", "y = y + g", "x")).IsEqualTo(p.Sym("h.f90", "y = x", "x"));
    }

    [Test]
    public async Task ImplicitTypingCreatesLocalsUnlessImplicitNone()
    {
        var p = TestSupport.Build("i.f", """
                  SUBROUTINE S
                  IMPLICIT DOUBLE PRECISION (A-H, O-Z)
                  X = 1
                  K = 2
                  END
                  SUBROUTINE T
                  IMPLICIT NONE
                  Y = 3
                  END
            """, Opts);
        var x = p.At("i.f", "X = 1", "X").Single();
        await Assert.That(x.Symbol).StartsWith("local ");
        await Assert.That(SymbolRole.Definition.Matches(x)).IsTrue();
        var info = p.Doc("i.f").Symbols.Single(s => s.Symbol == x.Symbol);
        await Assert.That(info.SignatureDocumentation.Text).IsEqualTo("double precision :: X");
        var k = p.Doc("i.f").Symbols.Single(s => s.Symbol == p.Sym("i.f", "K = 2", "K"));
        await Assert.That(k.SignatureDocumentation.Text).IsEqualTo("integer :: K");
        await Assert.That(p.Sym("i.f", "Y = 3", "Y")).IsNull();
    }

    [Test]
    public async Task GenericInterfacesAndOperators()
    {
        var p = TestSupport.Build("g.f90", """
            module ops
              implicit none
              type :: v
                real :: a
              end type
              interface swap
                module procedure swap_int, swap_real
              end interface
              interface operator(.cross.)
                module procedure cross
              end interface
            contains
              subroutine swap_int(a, b)
                integer :: a, b
              end subroutine
              subroutine swap_real(a, b)
                real :: a, b
              end subroutine
              function cross(x, y) result(z)
                type(v), intent(in) :: x, y
                type(v) :: z
                z%a = x%a * y%a
              end function
              subroutine use_them(p, q)
                type(v) :: p, q, r
                integer :: i, j
                call swap(i, j)
                r = p .cross. q
              end subroutine
            end module
            """, Opts);
        await Assert.That(p.Sym("g.f90", "call swap", "swap")).IsEqualTo(Pkg + "ops/swap().");
        await Assert.That(p.Sym("g.f90", "r = p .cross. q", ".cross.")).IsEqualTo(Pkg + "ops/`operator(.cross.)`().");
        var specific = p.Info(Pkg + "ops/swap_int().");
        await Assert.That(specific.Relationships.Single().Symbol).IsEqualTo(Pkg + "ops/swap().");
        await Assert.That(specific.Relationships.Single().IsImplementation).IsTrue();
        await Assert.That(p.Info(Pkg + "ops/swap().").Kind).IsEqualTo(SymbolInformation.Types.Kind.Interface);
        await Assert.That(p.Info(Pkg + "ops/`operator(.cross.)`().").Kind).IsEqualTo(SymbolInformation.Types.Kind.Operator);
    }

    [Test]
    public async Task GenericSharingANameWithItsSpecificIsDisambiguated()
    {
        var p = TestSupport.Build("g.f90", """
            module m
              interface norm
                module procedure norm
              end interface
            contains
              real function norm(x)
                real :: x(:)
                norm = sqrt(sum(x*x))
              end function
            end module
            """, Opts);
        await Assert.That(p.Sym("g.f90", "interface norm", "norm")).IsEqualTo(Pkg + "m/norm(generic).");
        await Assert.That(p.Sym("g.f90", "module procedure norm", "norm")).IsEqualTo(Pkg + "m/norm().");
        await Assert.That(TestSupport.Lint(p.Index)).IsEmpty();
    }

    [Test]
    public async Task TypeBoundProceduresAndComponentChains()
    {
        var p = TestSupport.Build("t.f90", """
            module zoo
              implicit none
              type :: inner_t
                integer :: n
              end type
              type :: animal
                type(inner_t) :: info
              contains
                procedure :: speak => animal_speak
              end type
              type, extends(animal) :: dog
              contains
                procedure :: speak => dog_speak
              end type
            contains
              subroutine animal_speak(self)
                class(animal), intent(in) :: self
              end subroutine
              subroutine dog_speak(self)
                class(dog), intent(in) :: self
                print *, self%info%n
              end subroutine
              subroutine run(d)
                type(dog) :: d
                call d%speak()
                d%animal%info%n = 1
              end subroutine
            end module
            """, Opts);
        await Assert.That(p.Sym("t.f90", "print *, self%info%n", "info")).IsEqualTo(Pkg + "zoo/animal#info.");
        await Assert.That(p.Sym("t.f90", "print *, self%info%n", "n")).IsEqualTo(Pkg + "zoo/inner_t#n.");
        await Assert.That(p.Sym("t.f90", "call d%speak", "speak")).IsEqualTo(Pkg + "zoo/dog#speak().");
        await Assert.That(p.Sym("t.f90", "d%animal%info", "animal")).IsEqualTo(Pkg + "zoo/animal#");
        var dog = p.Info(Pkg + "zoo/dog#");
        await Assert.That(dog.Relationships.Single().Symbol).IsEqualTo(Pkg + "zoo/animal#");
        await Assert.That(dog.Kind).IsEqualTo(SymbolInformation.Types.Kind.Class);
        var binding = p.Info(Pkg + "zoo/dog#speak().");
        await Assert.That(binding.Relationships.Single().Symbol).IsEqualTo(Pkg + "zoo/animal#speak().");
        var impl = p.Info(Pkg + "zoo/dog_speak().");
        await Assert.That(impl.Relationships.Single().Symbol).IsEqualTo(Pkg + "zoo/dog#speak().");
    }

    [Test]
    public async Task KeywordArgumentsAndIoSpecifiersAreNotVariables()
    {
        var p = TestSupport.Build("k.f90", """
            program k
              integer :: n, u
              call sub(count=n)
              open(unit=u, file='x', status='old')
              allocate(arr(n), stat=n)
            end program
            """, Opts);
        await Assert.That(p.Sym("k.f90", "call sub", "count")).IsNull();
        await Assert.That(p.Sym("k.f90", "open(", "unit")).IsNull();
        await Assert.That(p.Sym("k.f90", "open(", "u")).StartsWith("local ");
        await Assert.That(p.Sym("k.f90", "allocate", "stat")).IsNull();
        await Assert.That(p.Sym("k.f90", "call sub", "sub")).IsEqualTo(Pkg + "sub().");
        await Assert.That(p.Index.ExternalSymbols.Select(static s => s.Symbol)).Contains(Pkg + "sub().");
    }

    [Test]
    public async Task StatementFunctionsAndExternalFunctions()
    {
        var p = TestSupport.Build("s.f", """
                  DOUBLE PRECISION FUNCTION F(X)
                  DOUBLE PRECISION X, SQ, Z, G
                  EXTERNAL G
                  SQ(Z) = Z*Z
                  F = SQ(X) + G(X) + H(X)
                  END
            """, Opts);
        var sq = p.At("s.f", "SQ(Z) = Z*Z", "SQ").Single();
        await Assert.That(sq.Symbol).StartsWith("local ");
        await Assert.That(p.Sym("s.f", "F = SQ(X)", "SQ")).IsEqualTo(sq.Symbol);
        await Assert.That(p.Sym("s.f", "F = SQ(X)", "G")).IsEqualTo(Pkg + "g().");
        await Assert.That(p.Sym("s.f", "F = SQ(X)", "H")).IsEqualTo(Pkg + "h().");
        await Assert.That(p.Sym("s.f", "F = SQ(X)", "F")).IsEqualTo(Pkg + "f().");
        var ext = p.At("s.f", "EXTERNAL G", "G").Single();
        await Assert.That(SymbolRole.Definition.Matches(ext)).IsFalse();
    }

    [Test]
    public async Task FixedFormGluedKeywords()
    {
        var p = TestSupport.Build("glue.f", """
                  PROGRAM GLUE
                  CALLWORK(3)
                  DO10I=1,3
               10 CONTINUE
                  GOTO20
               20 STOP
                  END
                  SUBROUTINE WORK(N)
                  END
            """, Opts);
        await Assert.That(p.Sym("glue.f", "CALLWORK", "WORK", wholeWord: false)).IsEqualTo(Pkg + "work().");
        await Assert.That(p.Sym("glue.f", "DO10I", "10", wholeWord: false)).IsEqualTo(p.Sym("glue.f", "10 CONTINUE", "10"));
        await Assert.That(p.Sym("glue.f", "DO10I", "I", wholeWord: false)).StartsWith("local ");
        await Assert.That(p.Sym("glue.f", "GOTO20", "20", wholeWord: false)).IsEqualTo(p.Sym("glue.f", "20 STOP", "20"));
    }

    [Test]
    public async Task CommonBlocksAreSharedByName()
    {
        var p = TestSupport.Build("c.f", """
                  SUBROUTINE A
                  COMMON /BLK/ X, Y(10)
                  X = 1
                  END
                  SUBROUTINE B
                  COMMON /BLK/ X, Y(10)
                  SAVE /BLK/
                  Y(1) = X
                  END
            """, Opts);
        await Assert.That(p.Sym("c.f", "X = 1", "X")).IsEqualTo(Pkg + "`/blk/`#x.");
        await Assert.That(p.Sym("c.f", "Y(1) = X", "X")).IsEqualTo(Pkg + "`/blk/`#x.");
        await Assert.That(p.Sym("c.f", "SAVE /BLK/", "BLK")).IsEqualTo(Pkg + "`/blk/`#");
        await Assert.That(p.Info(Pkg + "`/blk/`#").DisplayName).IsEqualTo("/BLK/");
    }

    [Test]
    public async Task IncludeFilesAreIndexedThroughTheirIncluders()
    {
        var p = TestSupport.Build(new()
        {
            ["src/main.f"] = "      PROGRAM M\n      INCLUDE 'inc/sizes.inc'\n      REAL W(NSIZE)\n      W(1) = 0\n      END\n",
            ["src/inc/sizes.inc"] = "      INTEGER NSIZE\n      PARAMETER (NSIZE = 4)\n",
        }, Opts);
        await Assert.That(p.Index.Documents.Select(static d => d.RelativePath).Order().ToList())
            .IsEquivalentTo(new List<string> { "src/inc/sizes.inc", "src/main.f" });
        await Assert.That(p.Sym("src/main.f", "REAL W", "NSIZE")).IsEqualTo(Pkg + "`src/inc/sizes.inc`/nsize.");
        await Assert.That(p.IsDef("src/inc/sizes.inc", "INTEGER NSIZE", "NSIZE")).IsTrue();
        await Assert.That(TestSupport.Lint(p.Index)).IsEmpty();
    }

    [Test]
    public async Task InterfaceBodiesDoNotHostAssociateWithoutImport()
    {
        var p = TestSupport.Build("ib.f90", """
            module m
              implicit none
              type :: t
              end type
              interface
                subroutine ext(a, b)
                  import :: t
                  type(t) :: a
                  integer :: b
                end subroutine
              end interface
            end module
            """, Opts);
        var ext = p.At("ib.f90", "subroutine ext", "ext").Single();
        await Assert.That(ext.Symbol).IsEqualTo(Pkg + "ext().");
        await Assert.That(SymbolRole.ForwardDefinition.Matches(ext)).IsTrue();
        await Assert.That(p.Sym("ib.f90", "type(t) :: a", "t")).IsEqualTo(Pkg + "m/t#");
    }

    [Test]
    public async Task FpmManifestNamesThePackage()
    {
        var p = TestSupport.Build(new()
        {
            ["fpm.toml"] = "name = \"mylib\"\nversion = \"0.3.1\"\n[library]\nsource-dir = \"src\"\n",
            ["src/m.f90"] = "module m\nend module\n",
        });
        await Assert.That(p.Sym("src/m.f90", "module m", "m")).IsEqualTo("scip-fortran fpm mylib 0.3.1 m/");
    }

    [Test]
    public async Task PreprocessorBranchesAndMacros()
    {
        const string src = "#define NBUF 16\nprogram p\n#ifdef BIG\n  integer, parameter :: n = 100\n#else\n  integer, parameter :: n = 7\n#endif\n  x = n + NBUF\nend program\n";
        var all = TestSupport.Build("p.F90", src, Opts);
        await Assert.That(all.IsDef("p.F90", "n = 100", "n")).IsTrue();
        await Assert.That(all.IsDef("p.F90", "n = 7", "n")).IsTrue();
        await Assert.That(all.Sym("p.F90", "x = n + NBUF", "NBUF")).IsNull();
        await Assert.That(all.Sym("p.F90", "x = n + NBUF", "x")).StartsWith("local ");

        var eval = TestSupport.Build("p.F90", src, Opts with { Defines = new Dictionary<string, string> { ["BIG"] = "1" } });
        await Assert.That(eval.IsDef("p.F90", "n = 100", "n")).IsTrue();
        await Assert.That(eval.At("p.F90", "n = 7", "n")).IsEmpty();
    }

    [Test]
    public async Task MalformedInputIsTolerated()
    {
        var p = TestSupport.Build(new()
        {
            ["bad.f90"] = "module\nsubroutine (\n  end do\nend interface\ncall\nx = = 3\ntype ::\nif (\nend\nend\nend module zz\n",
            ["bad.f"] = "     $ orphan continuation\n      DO 10\n   10\n      END IF\n      GO TO (\n",
        }, Opts);
        await Assert.That(TestSupport.Lint(p.Index)).IsEmpty();
    }
}
