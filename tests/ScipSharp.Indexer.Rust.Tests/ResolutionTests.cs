namespace ScipSharp.Indexer.Rust.Tests;

using static ProjectFixture;

/// <summary>Ports of the cross-crate/reference <c>check_symbol</c> tests of <c>cli/scip.rs</c> plus resolution tests.</summary>
public class ResolutionTests
{
    private static Task<string?> Cursor(params (string, string)[] files) => SymbolAtCursor(Workspace(files));

    // The upstream fixtures use `crate:main deps:foo`; here `foo` is a path dependency of `main`.
    private static (string, string)[] TwoCrates(string main, string foo) =>
    [
        ("Cargo.toml", "[workspace]\nmembers = [\"workspace\", \"foo\"]\n"),
        ("workspace/Cargo.toml", Manifest("main", "0.1.0", "foo = { path = \"../foo\" }")),
        ("workspace/src/lib.rs", main),
        ("foo/Cargo.toml", Manifest("foo", "0.1.0")),
        ("foo/src/lib.rs", foo),
    ];

    [Test]
    public async Task Basic()
    {
        var s = await Cursor(TwoCrates(
            "use foo::example_mod::func;\nfn main() {\n    func$0();\n}\n",
            "pub mod example_mod {\n    pub fn func() {}\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo foo 0.1.0 example_mod/func().");
    }

    [Test]
    public async Task SymbolForFieldInStructLiteral()
    {
        var s = await Cursor(TwoCrates(
            "use foo::St;\nfn main() {\n    let x = St { a$0: 2 };\n}\n",
            "pub struct St {\n    pub a: i32,\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo foo 0.1.0 St#a.");
    }

    [Test]
    public async Task SymbolForParamInDependency()
    {
        var s = await Cursor(TwoCrates(
            "use foo::example_mod::func;\nfn main() {\n    func(42);\n}\n",
            "pub mod example_mod {\n    pub fn func(x$0: usize) {}\n}\n"));
        await Assert.That(s).IsEqualTo("local enclosed by rust-analyzer cargo foo 0.1.0 example_mod/func().");
    }

    [Test]
    public async Task SymbolForClosureParam()
    {
        var s = await Cursor(TwoCrates(
            "fn main() {}\n",
            "pub mod example_mod {\n    pub fn func() {\n        let f = |x$0: usize| {};\n    }\n}\n"));
        await Assert.That(s).IsEqualTo("local enclosed by rust-analyzer cargo foo 0.1.0 example_mod/func().");
    }

    [Test]
    public async Task GlobalSymbolForPubStruct()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "mod foo;\n\nfn main() {\n    let _bar = foo::Bar { i: 0 };\n}\n"),
            ("src/foo.rs", "pub struct Bar$0 {\n    pub i: i32,\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . foo/Bar#");
    }

    [Test]
    public async Task GlobalSymbolForPubStructReference()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "mod foo;\n\nfn main() {\n    let _bar = foo::Bar$0 { i: 0 };\n}\n"),
            ("src/foo.rs", "pub struct Bar {\n    pub i: i32,\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . foo/Bar#");
    }

    [Test]
    public async Task OperatorOverload()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "use core::ops::AddAssign;\n\nstruct S;\n\nimpl AddAssign for S {\n    fn add_assign(&mut self, _rhs: Self) {}\n}\n\nfn main() {\n    let mut s = S;\n    s +=$0 S;\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . impl#[S][`AddAssign<Self>`]add_assign().");
    }

    [Test]
    public async Task UseImportsAndAliases()
    {
        var files = new[]
        {
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "pub mod shapes {\n    pub struct Circle;\n    pub fn area() {}\n}\nuse shapes::Circle as C;\nuse shapes::{self, area};\nfn f() { let _c = C$0; area(); }\n"),
        };
        await Assert.That(await Cursor(files)).IsEqualTo("rust-analyzer cargo main . shapes/Circle#");
    }

    [Test]
    public async Task GlobImportAndSuper()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "pub fn helper() {}\nmod inner {\n    use super::*;\n    pub fn go() { helper$0(); }\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . helper().");
    }

    [Test]
    public async Task CrateRootPath()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "pub struct A;\nmod m {\n    pub fn f() -> crate::A$0 { crate::A }\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . A#");
    }

    [Test]
    public async Task MethodCallOnParameter()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "struct Foo;\nimpl Foo {\n    fn bar(&self) {}\n}\nfn f(x: Foo) {\n    x.bar$0();\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . impl#[Foo]bar().");
    }

    [Test]
    public async Task MethodCallOnLetFromConstructor()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "struct Foo;\nimpl Foo {\n    fn new() -> Foo { Foo }\n    fn bar(&self) {}\n}\nfn f() {\n    let x = Foo::new();\n    x.bar$0();\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . impl#[Foo]bar().");
    }

    [Test]
    public async Task MethodCallThroughSelfFieldAndSelfReturn()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "struct Inner;\nimpl Inner {\n    fn ping(&self) {}\n}\nstruct Outer {\n    inner: Inner,\n}\nimpl Outer {\n    fn go(&self) {\n        self.inner.ping$0();\n    }\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . impl#[Inner]ping().");
    }

    [Test]
    public async Task FieldAccessOnSelf()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "struct P {\n    x: i32,\n}\nimpl P {\n    fn get(&self) -> i32 { self.x$0 }\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . P#x.");
    }

    [Test]
    public async Task TupleStructFields()
    {
        var use = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "struct W(i32, u8);\nfn f(w: W) -> u8 { w.1$0 }\n"));
        await Assert.That(use).IsEqualTo("rust-analyzer cargo main . W#1.");
    }

    [Test]
    public async Task GenericParameters()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "fn id<T>(x: T$0) -> T { x }\n"));
        await Assert.That(s).IsEqualTo("local enclosed by rust-analyzer cargo main . id().");
    }

    [Test]
    public async Task GenericStructMethodReturnsParameterType()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "struct Bag<T> { item: T }\nimpl<T> Bag<T> {\n    fn get(&self) -> &T { &self.item }\n}\nstruct Apple;\nimpl Apple {\n    fn eat(&self) {}\n}\nfn f(b: Bag<Apple>) {\n    b.get().eat$0();\n}\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . impl#[Apple]eat().");
    }

    [Test]
    public async Task StdOptionVariantAndMethod()
    {
        var some = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "fn f() -> Option<i32> { Some$0(1) }\n"));
        await Assert.That(some).IsEqualTo("rust-analyzer cargo core https://github.com/rust-lang/rust/library/core option/Option#Some#");
        var unwrap = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "fn f(o: Option<i32>) -> i32 { o.unwrap$0() }\n"));
        await Assert.That(unwrap).IsEqualTo("rust-analyzer cargo core https://github.com/rust-lang/rust/library/core option/impl#[`Option<T>`]unwrap().");
    }

    [Test]
    public async Task StdVecAndHashMapImports()
    {
        var push = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "fn f() {\n    let mut v = Vec::new();\n    v.push$0(1);\n}\n"));
        await Assert.That(push).IsNotNull();
        await Assert.That(push!).Contains("vec/impl#[`Vec<T, A>`]push().");
        var map = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "use std::collections::HashMap$0;\n"));
        await Assert.That(map).IsEqualTo("rust-analyzer cargo std https://github.com/rust-lang/rust/library/std collections/hash/map/HashMap#");
    }

    [Test]
    public async Task ExternalSymbolsAreEmittedForStdReferences()
    {
        var (index, _) = await IndexAsync(Workspace(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "fn f() -> Option<String> { None }\n")));
        var ext = index.ExternalSymbols.Select(e => e.Symbol).ToList();
        await Assert.That(ext.Any(x => x.EndsWith("option/Option#", StringComparison.Ordinal))).IsTrue();
        await Assert.That(ext.Any(x => x.EndsWith("string/String#", StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task ForLoopAndIfLetBindingsAreLocals()
    {
        var (index, _) = await IndexAsync(Workspace(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "fn f(v: Vec<i32>, o: Option<i32>) {\n    for item in v { let _y = item; }\n    if let Some(n) = o { let _z = n; }\n}\n")));
        var names = index.Documents.Single().Symbols.Where(s => s.Symbol.StartsWith("local ", StringComparison.Ordinal)).Select(s => s.DisplayName).ToList();
        await Assert.That(names).Contains("item");
        await Assert.That(names).Contains("n");
    }

    [Test]
    public async Task RootModuleIsCrateAndDescendantsHaveNoCratePrefix()
    {
        var (index, _) = await IndexAsync(Workspace(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "pub mod a { pub struct S; }\n")));
        var symbols = index.Documents.Single().Symbols.Select(s => s.Symbol).ToList();
        await Assert.That(symbols).Contains("rust-analyzer cargo main . crate/");
        await Assert.That(symbols).Contains("rust-analyzer cargo main . a/S#");
    }

    [Test]
    public async Task DeriveAndTestAttributesReferenceCoreMacros()
    {
        var derive = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "#[derive(Debug$0)]\nstruct S;\n"));
        await Assert.That(derive).IsEqualTo("rust-analyzer cargo core https://github.com/rust-lang/rust/library/core fmt/macros/Debug:");
        var test = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "#[test$0]\nfn t() {}\n"));
        await Assert.That(test).IsEqualTo("rust-analyzer cargo core https://github.com/rust-lang/rust/library/core macros/builtin/test:");
    }

    [Test]
    public async Task MatchArmBindingsAreLocals()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "fn f(o: Option<i32>) -> i32 { match o { Some(n) => n$0, None => 0 } }\n"));
        await Assert.That(s).StartsWith("local ");
    }

    [Test]
    public async Task TestGeneratingMacroDefinesFunction()
    {
        var (index, _) = await IndexAsync(Workspace(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "macro_rules! mk { ($name:ident, $f:expr) => { #[test] fn $name() { $f(1); } }; }\nmk!(my_test, |x: i32| { let _ = x; });\n")));
        await Assert.That(index.Documents.Single().Symbols.Select(s => s.Symbol)).Contains("rust-analyzer cargo main . my_test().");
    }

    [Test]
    public async Task PrimitiveOperatorsAndTryResolveToCoreImpls()
    {
        var plus = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "fn f(a: usize, b: usize) -> usize { a +$0 b }\n"));
        await Assert.That(plus).IsEqualTo("rust-analyzer cargo core https://github.com/rust-lang/rust/library/core ops/arith/impl#[usize][`Add<Self>`]add().");
        var question = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "fn g() -> Result<u8, ()> { Ok(1) }\nfn f() -> Result<u8, ()> { g()?$0 ; Ok(2) }\n"));
        await Assert.That(question).IsEqualTo("rust-analyzer cargo core https://github.com/rust-lang/rust/library/core result/impl#[`Result<T, E>`][Try]branch().");
    }

    [Test]
    public async Task MethodCallPrefersMethodOverFieldOfSameName()
    {
        var s = await Cursor(
            ("Cargo.toml", Manifest("main")),
            ("src/lib.rs", "struct B { yes: bool }\nimpl B { fn yes(&mut self) -> bool { self.yes } }\nfn f(b: &mut B) -> bool { b.yes$0() }\n"));
        await Assert.That(s).IsEqualTo("rust-analyzer cargo main . impl#[B]yes().");
    }
}
