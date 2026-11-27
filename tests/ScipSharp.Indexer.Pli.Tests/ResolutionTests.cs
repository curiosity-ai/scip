using static ScipSharp.Indexer.Pli.Tests.TestHelpers;

namespace ScipSharp.Indexer.Pli.Tests;

/// <summary>Declarations, scoping and name resolution.</summary>
public class ResolutionTests
{
    private const string Pkg = "scip-pli pli t . ";

    private static Index Run(string source) => IndexSource(source, "--package-name=t");

    [Test]
    public async Task FactoredDeclarationsShareAttributes()
    {
        const string src = "P: PROC; DCL (A, (B, C) FIXED) BIN(31), D CHAR(1); A = B + C; END;";
        var index = Run(src);
        var doc = index.Documents.Single();
        var symbols = doc.Symbols.ToDictionary(static s => s.DisplayName);
        await Assert.That(symbols["A"].Documentation[0]).Contains("DCL A BIN(31)");
        await Assert.That(symbols["B"].Documentation[0]).Contains("DCL B FIXED BIN(31)");
        await Assert.That(symbols["D"].Documentation[0]).Contains("DCL D CHAR(1)");
        await Assert.That(SymbolsAt(index, src, "C")).IsEquivalentTo(new[] { Pkg + "P().C.", Pkg + "P().C." });
    }

    [Test]
    public async Task StructureLevelsAndFactoredMembers()
    {
        const string src = "P: PROC; DCL 1 S, 2 (X, Y) FIXED, 2 T, 3 U CHAR(2), 2 Z; S.T.U = 'AB'; X = Z; END;";
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "U")).IsEquivalentTo(new[] { Pkg + "P().S.T.U.", Pkg + "P().S.T.U." });
        await Assert.That(SymbolsAt(index, src, "Y")).IsEquivalentTo(new[] { Pkg + "P().S.Y." });
        await Assert.That(SymbolsAt(index, src, "Z")).IsEquivalentTo(new[] { Pkg + "P().S.Z.", Pkg + "P().S.Z." });
        var info = index.Documents.Single().Symbols.Single(static s => s.DisplayName == "U");
        await Assert.That(info.Kind).IsEqualTo(SymbolInformation.Types.Kind.Field);
    }

    [Test]
    public async Task PartiallyQualifiedReferencesPickTheUniqueMatch()
    {
        const string src = """
            P: PROC;
              DCL 1 A, 2 K, 3 ID CHAR(5), 2 DATA CHAR(5);
              DCL 1 B, 2 K, 3 ID CHAR(5);
              A.ID = B.K.ID;
              DATA = A.K.ID;
            END;
            """;
        var index = Run(src);
        var ids = SymbolsAt(index, src, "ID");
        await Assert.That(ids).IsEquivalentTo(new[] { Pkg + "P().A.K.ID.", Pkg + "P().B.K.ID.", Pkg + "P().A.K.ID.", Pkg + "P().B.K.ID.", Pkg + "P().A.K.ID." });
        await Assert.That(SymbolsAt(index, src, "DATA")[1]).IsEqualTo(Pkg + "P().A.DATA.");
    }

    [Test]
    public async Task LikeAndTypeTemplatesResolveMembers()
    {
        const string src = """
            P: PROC;
              DEFINE STRUCTURE 1 PT, 2 X FIXED, 2 Y FIXED;
              DCL 1 TEMPLATE, 2 F CHAR(1);
              DCL 1 COPY LIKE TEMPLATE;
              DCL ORIGIN TYPE(PT);
              COPY.F = 'A';
              ORIGIN.Y = 1;
            END;
            """;
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "F")[1]).IsEqualTo(Pkg + "P().TEMPLATE.F.");
        await Assert.That(SymbolsAt(index, src, "Y")[1]).IsEqualTo(Pkg + "P().PT#Y.");
    }

    [Test]
    public async Task InnerBlocksHideOuterDeclarations()
    {
        const string src = """
            OUTER: PROC;
              DCL N FIXED;
              DCL M FIXED;
              INNER: PROC;
                DCL N CHAR(1);
                N = 'X';
                M = 2;
              END INNER;
              B: BEGIN;
                DCL M FLOAT;
                M = N;
              END B;
              CALL INNER;
            END OUTER;
            """;
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "N")).IsEquivalentTo(new[] { Pkg + "OUTER().N.", Pkg + "OUTER().INNER().N.", Pkg + "OUTER().INNER().N.", Pkg + "OUTER().N." });
        await Assert.That(SymbolsAt(index, src, "M")).IsEquivalentTo(new[] { Pkg + "OUTER().M.", Pkg + "OUTER().M.", Pkg + "OUTER().B/M.", Pkg + "OUTER().B/M." });
        await Assert.That(SymbolsAt(index, src, "INNER")).IsEquivalentTo(new[] { Pkg + "OUTER().INNER().", Pkg + "OUTER().INNER().", Pkg + "OUTER().INNER()." });
    }

    [Test]
    public async Task DeclarationsApplyToTheWholeBlock()
    {
        const string src = "P: PROC; X = 1; DCL X FIXED BIN; END;";
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "X")).IsEquivalentTo(new[] { Pkg + "P().X.", Pkg + "P().X." });
    }

    [Test]
    public async Task UndeclaredNamesAreImplicitLocals()
    {
        const string src = "P: PROC; Q: PROC; I = 1; END; DO I = 1 TO 3; END; CALL Q; END;";
        var index = Run(src);
        var symbols = SymbolsAt(index, src, "I");
        await Assert.That(symbols.Count).IsEqualTo(2);
        await Assert.That(symbols.Distinct().Single()).StartsWith("local ");
        var info = index.Documents.Single().Symbols.Single(s => s.Symbol == symbols[0]);
        await Assert.That(info.Documentation[0]).Contains("implicitly declared");
    }

    [Test]
    public async Task KeywordsAreNotReserved()
    {
        const string src = "P: PROC; DCL (IF, THEN, ELSE, DO) FIXED; IF IF = 1 THEN THEN = 2; ELSE ELSE = DO; END;";
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "THEN")).IsEquivalentTo(new[] { Pkg + "P().THEN.", Pkg + "P().THEN." });
        await Assert.That(SymbolsAt(index, src, "ELSE")).IsEquivalentTo(new[] { Pkg + "P().ELSE.", Pkg + "P().ELSE." });
        await Assert.That(SymbolsAt(index, src, "DO")).IsEquivalentTo(new[] { Pkg + "P().DO.", Pkg + "P().DO." });
    }

    [Test]
    public async Task StatementKeywordsAndBuiltinsAreNotImplicitDeclarations()
    {
        const string src = "P: PROC; DCL S CHAR(9); PUT SKIP LIST(SUBSTR(S, 1, 2)); PUT EDIT (S) (A(9)); OPEN FILE(SYSPRINT) OUTPUT; END;";
        var index = Run(src);
        var occ = Occurrences(index, src);
        await Assert.That(occ.Where(static o => o.Symbol.StartsWith("local", StringComparison.Ordinal))).IsEmpty();
        await Assert.That(SymbolsAt(index, src, "SUBSTR")).IsEquivalentTo(new[] { "scip-pli pli builtin . SUBSTR()." });
        await Assert.That(SymbolsAt(index, src, "SYSPRINT")).IsEquivalentTo(new[] { "scip-pli pli builtin . SYSPRINT." });
        await Assert.That(index.ExternalSymbols.Select(static s => s.Symbol)).Contains("scip-pli pli builtin . SUBSTR().");
    }

    [Test]
    public async Task ParametersAreDefinedByTheirDeclaration()
    {
        const string src = "P: PROC (A, B); DCL A FIXED; E: ENTRY (B, C); DCL C CHAR(1); END;";
        var index = Run(src);
        var a = Occurrences(index, src).Where(static o => o.Text == "A").ToList();
        await Assert.That(a.Select(static o => o.Symbol).Distinct().Single()).IsEqualTo(Pkg + "P().(A)");
        await Assert.That(a.Select(static o => o.Definition)).IsEquivalentTo(new[] { false, true });
        await Assert.That(SymbolsAt(index, src, "B").Distinct().Single()).IsEqualTo(Pkg + "P().(B)");
        await Assert.That(SymbolsAt(index, src, "C").Distinct().Single()).IsEqualTo(Pkg + "P().(C)");
        await Assert.That(SymbolsAt(index, src, "E").Single()).IsEqualTo(Pkg + "E().");
    }

    [Test]
    public async Task LabelsGotoAndMultipleClosure()
    {
        const string src = """
            P: PROC;
              L1: DO I = 1 TO 2;
                L2: DO;
                  IF I = 2 THEN GO TO OUT;
              END L1;
            OUT: RETURN;
            END P;
            """;
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "L1")).IsEquivalentTo(new[] { Pkg + "P().L1.", Pkg + "P().L1." });
        await Assert.That(SymbolsAt(index, src, "OUT")).IsEquivalentTo(new[] { Pkg + "P().OUT.", Pkg + "P().OUT." });
        var proc = index.Documents.Single().Occurrences.Single(static o => o.Symbol == Pkg + "P()." && (o.SymbolRoles & 1) != 0);
        await Assert.That(proc.GetEnclosingSourceRange()!.Value.End.Line).IsEqualTo(6);
    }

    [Test]
    public async Task BuiltinDeclarationsReferenceTheBuiltin()
    {
        const string src = "P: PROC; DCL (ADDR, NULL) BUILTIN; DCL Q PTR INIT(NULL()); Q = ADDR(Q); END;";
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "ADDR").Distinct().Single()).IsEqualTo("scip-pli pli builtin . ADDR().");
        await Assert.That(index.Documents.Single().Symbols.Any(static s => s.Symbol.Contains("ADDR", StringComparison.Ordinal))).IsFalse();
    }

    [Test]
    public async Task AttributeKeywordsDeclaredBuiltinStayAttributes()
    {
        const string src = "p: proc; dcl (fixed, binary) builtin; dcl x fixed bin (35); dcl y fixed; y = fixed (binary (x), 17); end;";
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "fixed")).IsEquivalentTo(new[] { "scip-pli pli builtin . FIXED().", "scip-pli pli builtin . FIXED()." });
    }

    [Test]
    public async Task ExternalProceduresLinkAcrossFiles()
    {
        var files = new Dictionary<string, string>
        {
            ["main.pli"] = "MAIN: PROC OPTIONS(MAIN); DCL HELPER ENTRY (FIXED); DCL SHARED FIXED EXT; CALL HELPER(1); CALL UTIL$ALT; CALL MISSING; END;",
            ["helper.pli"] = "HELPER: PROC (N); DCL N FIXED; DCL SHARED FIXED EXTERNAL; SHARED = N; END;",
            ["util.pli"] = "UTIL: PROC; ALT: ENTRY; END;",
        };
        var index = IndexFiles(files, "--margins=none", "--package-name=t");
        var main = index.Documents.Single(static d => d.RelativePath == "main.pli");
        var symbols = main.Occurrences.Select(static o => o.Symbol).ToList();
        await Assert.That(symbols).Contains(Pkg + "HELPER().");
        await Assert.That(symbols).Contains(Pkg + "SHARED.");
        await Assert.That(symbols).Contains(Pkg + "ALT().");
        await Assert.That(symbols).Contains(Pkg + "MISSING().");

        // HELPER is defined in helper.pli; the DECLARE in main.pli is only a reference.
        await Assert.That(main.Symbols.Any(static s => s.Symbol == Pkg + "HELPER().")).IsFalse();
        await Assert.That(main.Symbols.Single(static s => s.Symbol == Pkg + "MISSING().").Documentation[0]).Contains("implicitly declared");
        var helper = index.Documents.Single(static d => d.RelativePath == "helper.pli");
        await Assert.That(helper.Occurrences.Any(static o => o.Symbol == Pkg + "HELPER()." && (o.SymbolRoles & 1) != 0)).IsTrue();
        await Assert.That(Lint(index)).IsEmpty();
    }

    [Test]
    public async Task IncludeMembersAreResolvedAndIndexedInTheirOwnDocument()
    {
        var files = new Dictionary<string, string>
        {
            ["src/main.pli"] = "MAIN: PROC; %INCLUDE SYSLIB(RECDEF); %include 'other.inc'; REC.F = 1; G = 2; END;",
            ["copy/recdef.cpy"] = "DCL 1 REC, 2 F FIXED;",
            ["src/other.inc"] = "DCL G FIXED;",
        };
        var index = IndexFiles(files, "--margins=none", "--package-name=t", "--include-dir=copy");
        var paths = index.Documents.Select(static d => d.RelativePath).Order(StringComparer.Ordinal).ToList();
        await Assert.That(paths).IsEquivalentTo(new[] { "copy/recdef.cpy", "src/main.pli", "src/other.inc" });
        var rec = index.Documents.Single(static d => d.RelativePath == "copy/recdef.cpy");
        await Assert.That(rec.Occurrences.Select(static o => o.Symbol)).Contains(Pkg + "MAIN().REC.F.");
        var main = index.Documents.Single(static d => d.RelativePath == "src/main.pli");
        await Assert.That(main.Occurrences.Select(static o => o.Symbol)).Contains(Pkg + "MAIN().G.");
        await Assert.That(Lint(index)).IsEmpty();
    }

    [Test]
    public async Task PackagesExportProcedures()
    {
        const string src = "PK: PACKAGE EXPORTS(F); DCL V FIXED; F: PROC; V = 1; END F; G: PROC; CALL F; END G; END PK;";
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "F")).IsEquivalentTo(new[] { Pkg + "PK/F().", Pkg + "PK/F().", Pkg + "PK/F().", Pkg + "PK/F()." });
        await Assert.That(SymbolsAt(index, src, "V").Distinct().Single()).IsEqualTo(Pkg + "PK/V.");
    }

    [Test]
    public async Task PreprocessorVariablesAndProcedures()
    {
        const string src = "%DCL N FIXED; %N = 3; %DCL TWICE ENTRY; %TWICE: PROC(X) RETURNS(FIXED); DCL X FIXED; RETURN(X*2); %END; P: PROC; DCL A(N) FIXED; A(1) = TWICE(N); END;";
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "N").Distinct().Single()).IsEqualTo(Pkg + "`test.pli`/N!");
        await Assert.That(SymbolsAt(index, src, "TWICE").Distinct().Single()).IsEqualTo(Pkg + "`test.pli`/TWICE!");
        await Assert.That(SymbolsAt(index, src, "X").Distinct().Single()).IsEqualTo(Pkg + "`test.pli`/TWICE!(X)");
    }

    [Test]
    public async Task ConditionPrefixesOnUnitsAndSql()
    {
        const string src = """
            P: PROC;
              DCL EOF BIT(1);
              DCL HOSTVAR CHAR(8);
              (SIZE, NOSUBRG): X = 1;
              ON ENDFILE(SYSIN) BEGIN; EOF = '1'B; END;
              ON CONDITION(MINE) SNAP SYSTEM;
              EXEC SQL SELECT NAME INTO :HOSTVAR FROM EMP;
            END;
            """;
        var index = Run(src);
        await Assert.That(SymbolsAt(index, src, "EOF").Count).IsEqualTo(2);
        await Assert.That(SymbolsAt(index, src, "HOSTVAR").Distinct().Single()).IsEqualTo(Pkg + "P().HOSTVAR.");
        await Assert.That(SymbolsAt(index, src, "NAME")).IsEmpty();
        await Assert.That(SymbolsAt(index, src, "SIZE")).IsEmpty();
        await Assert.That(Lint(index)).IsEmpty();
    }

    [Test]
    public async Task CaseInsensitiveByDefaultSensitiveOnRequest()
    {
        const string src = "p: proc; dcl Abc fixed; ABC = abc; end;";
        var insensitive = Run(src);
        await Assert.That(SymbolsAt(insensitive, src, "abc").Distinct().Single()).IsEqualTo(Pkg + "P().ABC.");
        var sensitive = IndexSource(src, "--package-name=t", "--case-sensitive");
        var symbols = Occurrences(sensitive, src).Where(static o => o.Text.Equals("abc", StringComparison.OrdinalIgnoreCase)).Select(static o => o.Symbol).ToList();
        await Assert.That(symbols[0]).IsEqualTo(Pkg + "p().Abc.");
        await Assert.That(symbols[1]).StartsWith("local");
    }

    [Test]
    public async Task DeeplyNestedPrefixesDoNotOverflowTheStack()
    {
        var src = "P: PROC; DCL X FIXED; " + string.Concat(Enumerable.Repeat("IF X THEN ELSE ", 20_000)) + "X = 1; END;";
        var index = Run(src);
        await Assert.That(index.Documents.Count).IsEqualTo(1);
        await Assert.That(Lint(index)).IsEmpty();
    }

    [Test]
    public async Task ErrorToleranceOnGarbage()
    {
        const string src = "P: PROC; DCL (A, B FIXED; END END END; ))) ; X = ; CALL ; GO TO ; DCL 1 ; %INCLUDE NOWHERE; Q: PROC";
        var index = Run(src);
        await Assert.That(index.Documents.Count).IsEqualTo(1);
        await Assert.That(Lint(index)).IsEmpty();
    }
}
