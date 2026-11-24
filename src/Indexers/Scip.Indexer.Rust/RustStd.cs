namespace Scip.Indexer.Rust;

/// <summary>
/// A small built-in model of the standard library (core/alloc/std): where well-known items are defined (so symbols
/// match rust-analyzer's, e.g. <c>core/option/Option#</c>), their re-exports through <c>std</c>, the prelude, and
/// the signatures of the most commonly used methods (return types drive method-call resolution on locals).
/// Line format: <c>kind|path|selfText|variants|members</c>; members are <c>name(s)-&gt;Ret</c> (method with self) or
/// <c>name()-&gt;Ret</c> (associated function).
/// </summary>
internal static class RustStd
{
    /// <summary>The library version string rust-analyzer reports for sysroot crates is a repository URL.</summary>
    public const string RepoUrl = "https://github.com/rust-lang/rust/library/";

    /// <summary>Operator traits whose defaulted <c>Rhs = Self</c> parameter rust-analyzer displays.</summary>
    public static readonly HashSet<string> DefaultSelfArgTraits =
    [
        "Add", "Sub", "Mul", "Div", "Rem", "BitAnd", "BitOr", "BitXor", "Shl", "Shr",
        "AddAssign", "SubAssign", "MulAssign", "DivAssign", "RemAssign", "BitAndAssign", "BitOrAssign", "BitXorAssign",
        "ShlAssign", "ShrAssign", "PartialEq", "PartialOrd",
        "ops::Add", "ops::Sub", "ops::Mul", "ops::Div", "ops::AddAssign", "ops::SubAssign", "ops::MulAssign", "ops::DivAssign",
        "ops::Rem", "cmp::PartialEq", "cmp::PartialOrd",
    ];

    /// <summary>Operator token to (trait name, method name).</summary>
    public static readonly Dictionary<string, (string Trait, string Method)> Operators = new()
    {
        ["+="] = ("AddAssign", "add_assign"), ["-="] = ("SubAssign", "sub_assign"), ["*="] = ("MulAssign", "mul_assign"),
        ["/="] = ("DivAssign", "div_assign"), ["%="] = ("RemAssign", "rem_assign"), ["+"] = ("Add", "add"),
        ["-"] = ("Sub", "sub"), ["*"] = ("Mul", "mul"), ["/"] = ("Div", "div"), ["%"] = ("Rem", "rem"),
        ["&="] = ("BitAndAssign", "bitand_assign"), ["|="] = ("BitOrAssign", "bitor_assign"), ["^="] = ("BitXorAssign", "bitxor_assign"),
        ["<<="] = ("ShlAssign", "shl_assign"), [">>="] = ("ShrAssign", "shr_assign"),
    };

    public const string Data = """
        enum|core/option/Option|Option<T>|Some;None|unwrap(s)->T;expect(s)->T;is_some(s)->bool;is_none(s)->bool;map(s)->Option<U>;unwrap_or(s)->T;unwrap_or_else(s)->T;unwrap_or_default(s)->T;ok_or(s)->Result<T,E>;ok_or_else(s)->Result<T,E>;and_then(s)->Option<U>;as_ref(s)->Option<T>;as_mut(s)->Option<T>;take(s)->Option<T>;cloned(s)->Option<T>;copied(s)->Option<T>;filter(s)->Option<T>;or(s)->Option<T>;or_else(s)->Option<T>;replace(s)->Option<T>;as_deref(s)->Option<T>;iter(s)->Iter<T>
        enum|core/result/Result|Result<T, E>|Ok;Err|unwrap(s)->T;expect(s)->T;unwrap_err(s)->E;is_ok(s)->bool;is_err(s)->bool;map(s)->Result<U,E>;map_err(s)->Result<T,F>;ok(s)->Option<T>;err(s)->Option<E>;unwrap_or(s)->T;unwrap_or_else(s)->T;unwrap_or_default(s)->T;and_then(s)->Result<U,E>;as_ref(s)->Result<T,E>
        struct|alloc/vec/Vec|Vec<T>||new()->Vec<T>;with_capacity()->Vec<T>
        impl|alloc/vec/Vec|Vec<T, A>|alloc/vec|push(s)->();pop(s)->Option<T>;len(s)->usize;is_empty(s)->bool;clear(s)->();insert(s)->();remove(s)->T;truncate(s)->();extend(s)->();append(s)->();drain(s)->Iter<T>;retain(s)->();capacity(s)->usize;reserve(s)->();swap_remove(s)->T;extend_from_slice(s)->();dedup(s)->();into_iter(s)->Iter<T>;as_slice(s)->[T];split_off(s)->Vec<T>
        impl|alloc/vec/Vec|[T]|core/slice|iter(s)->Iter<T>;iter_mut(s)->Iter<T>;get(s)->Option<T>;get_mut(s)->Option<T>;first(s)->Option<T>;last(s)->Option<T>;contains(s)->bool;sort(s)->();sort_by(s)->();sort_by_key(s)->();reverse(s)->();join(s)->String;concat(s)->Vec<T>;to_vec(s)->Vec<T>;windows(s)->Iter<[T]>;chunks(s)->Iter<[T]>;split_at(s)->();swap(s)->();binary_search(s)->Result<usize,usize>;starts_with(s)->bool;ends_with(s)->bool;fill(s)->()
        struct|alloc/string/String|String||new()->String;with_capacity()->String;from_utf8()->Result<String,E>;from_utf8_lossy()->String
        impl|alloc/string/String|String|alloc/string|push_str(s)->();push(s)->();len(s)->usize;is_empty(s)->bool;as_str(s)->str;clear(s)->();capacity(s)->usize;into_bytes(s)->Vec<T>;insert(s)->();pop(s)->Option<T>;truncate(s)->();remove(s)->();reserve(s)->();into_boxed_str(s)->str;insert_str(s)->()
        impl|alloc/string/String|str|core/str|chars(s)->Iter<T>;bytes(s)->Iter<T>;lines(s)->Iter<str>;split(s)->Iter<str>;trim(s)->str;trim_start(s)->str;trim_end(s)->str;contains(s)->bool;starts_with(s)->bool;ends_with(s)->bool;to_lowercase(s)->String;to_uppercase(s)->String;parse(s)->Result<T,E>;find(s)->Option<usize>;replace(s)->String;as_bytes(s)->[T];split_whitespace(s)->Iter<str>;char_indices(s)->Iter<T>;to_owned(s)->String;repeat(s)->String;is_char_boundary(s)->bool;strip_prefix(s)->Option<str>;strip_suffix(s)->Option<str>;split_once(s)->Option<T>;eq_ignore_ascii_case(s)->bool
        struct|alloc/boxed/Box|Box<T>||new()->Box<T>
        struct|alloc/rc/Rc|Rc<T>||new()->Rc<T>;clone()->Rc<T>;strong_count()->usize;ptr_eq()->bool
        struct|alloc/sync/Arc|Arc<T>||new()->Arc<T>;clone()->Arc<T>;strong_count()->usize;ptr_eq()->bool
        struct|core/cell/RefCell|RefCell<T>||new()->RefCell<T>
        impl|core/cell/RefCell|RefCell<T>|core/cell|borrow(s)->Ref<T>;borrow_mut(s)->Ref<T>;into_inner(s)->T;replace(s)->T
        struct|core/cell/Ref|Ref<T>||
        struct|core/cell/Cell|Cell<T>||new()->Cell<T>
        impl|core/cell/Cell|Cell<T>|core/cell|get(s)->T;set(s)->();replace(s)->T;take(s)->T
        struct|std/collections/hash/map/HashMap|HashMap<K, V>||new()->HashMap<K,V>;with_capacity()->HashMap<K,V>
        impl|std/collections/hash/map/HashMap|HashMap<K, V, S>|std/collections/hash/map|insert(s)->Option<V>;get(s)->Option<V>;get_mut(s)->Option<V>;contains_key(s)->bool;remove(s)->Option<V>;entry(s)->Entry<K,V>;len(s)->usize;is_empty(s)->bool;iter(s)->Iter<K>;keys(s)->Iter<K>;values(s)->Iter<V>;values_mut(s)->Iter<V>;clear(s)->();into_iter(s)->Iter<K>;extend(s)->();retain(s)->()
        struct|std/collections/hash/map/Entry|Entry<K, V>||
        impl|std/collections/hash/map/Entry|Entry<K, V>|std/collections/hash/map|or_insert(s)->V;or_insert_with(s)->V;or_default(s)->V;and_modify(s)->Entry<K,V>
        struct|std/collections/hash/set/HashSet|HashSet<T>||new()->HashSet<T>;with_capacity()->HashSet<T>
        impl|std/collections/hash/set/HashSet|HashSet<T, S>|std/collections/hash/set|insert(s)->bool;contains(s)->bool;remove(s)->bool;len(s)->usize;is_empty(s)->bool;iter(s)->Iter<T>;clear(s)->();extend(s)->();union(s)->Iter<T>;intersection(s)->Iter<T>;difference(s)->Iter<T>
        struct|alloc/collections/btree/map/BTreeMap|BTreeMap<K, V>||new()->BTreeMap<K,V>
        impl|alloc/collections/btree/map/BTreeMap|BTreeMap<K, V, A>|alloc/collections/btree/map|insert(s)->Option<V>;get(s)->Option<V>;get_mut(s)->Option<V>;contains_key(s)->bool;remove(s)->Option<V>;entry(s)->Entry<K,V>;len(s)->usize;is_empty(s)->bool;iter(s)->Iter<K>;keys(s)->Iter<K>;values(s)->Iter<V>
        struct|alloc/collections/btree/set/BTreeSet|BTreeSet<T>||new()->BTreeSet<T>
        impl|alloc/collections/btree/set/BTreeSet|BTreeSet<T, A>|alloc/collections/btree/set|insert(s)->bool;contains(s)->bool;remove(s)->bool;len(s)->usize;is_empty(s)->bool;iter(s)->Iter<T>
        struct|alloc/collections/vec_deque/VecDeque|VecDeque<T>||new()->VecDeque<T>
        impl|alloc/collections/vec_deque/VecDeque|VecDeque<T, A>|alloc/collections/vec_deque|push_back(s)->();push_front(s)->();pop_back(s)->Option<T>;pop_front(s)->Option<T>;len(s)->usize;is_empty(s)->bool;iter(s)->Iter<T>;front(s)->Option<T>;back(s)->Option<T>;clear(s)->()
        struct|core/slice/iter/Iter|Iter<'a, T>||
        struct|core/time/Duration|Duration||from_secs()->Duration;from_millis()->Duration
        trait|core/clone/Clone||clone(s)->Self;clone_from(s)->()
        trait|core/default/Default||default()->Self
        trait|core/fmt/Debug||fmt(s)->Result<T,E>
        trait|core/fmt/Display||fmt(s)->Result<T,E>
        trait|core/fmt/Write||write_str(s)->Result<T,E>;write_fmt(s)->Result<T,E>
        struct|core/fmt/Formatter|Formatter<'a>||
        impl|core/fmt/Formatter|Formatter<'a>|core/fmt|write_str(s)->Result<T,E>;write_fmt(s)->Result<T,E>;debug_struct(s)->();pad(s)->Result<T,E>
        trait|core/convert/From||from()->Self
        trait|core/convert/Into||into(s)->T
        trait|core/convert/AsRef||as_ref(s)->T
        trait|core/convert/AsMut||as_mut(s)->T
        trait|core/convert/TryFrom||try_from()->Result<T,E>
        trait|core/convert/TryInto||try_into(s)->Result<T,E>
        trait|core/cmp/PartialEq||eq(s)->bool;ne(s)->bool
        trait|core/cmp/Eq||
        trait|core/cmp/PartialOrd||partial_cmp(s)->Option<T>;lt(s)->bool;le(s)->bool;gt(s)->bool;ge(s)->bool
        trait|core/cmp/Ord||cmp(s)->T;max(s)->Self;min(s)->Self
        trait|core/hash/Hash||hash(s)->()
        trait|core/iter/traits/iterator/Iterator||next(s)->Option<T>;map(s)->Iter<U>;filter(s)->Iter<T>;collect(s)->B;count(s)->usize;enumerate(s)->Iter<U>;zip(s)->Iter<U>;rev(s)->Iter<T>;sum(s)->B;fold(s)->B;any(s)->bool;all(s)->bool;find(s)->Option<T>;for_each(s)->();take(s)->Iter<T>;skip(s)->Iter<T>;cloned(s)->Iter<T>;copied(s)->Iter<T>;chain(s)->Iter<T>;flat_map(s)->Iter<U>;filter_map(s)->Iter<U>;last(s)->Option<T>;min(s)->Option<T>;max(s)->Option<T>;peekable(s)->Iter<T>;nth(s)->Option<T>;position(s)->Option<usize>;take_while(s)->Iter<T>;skip_while(s)->Iter<T>;step_by(s)->Iter<T>;unzip(s)->B;product(s)->B;min_by_key(s)->Option<T>;max_by_key(s)->Option<T>;by_ref(s)->Iter<T>
        trait|core/iter/traits/collect/IntoIterator||into_iter(s)->Iter<T>
        trait|core/iter/traits/collect/FromIterator||from_iter()->Self
        trait|core/iter/traits/collect/Extend||extend(s)->()
        trait|alloc/string/ToString||to_string(s)->String
        trait|alloc/borrow/ToOwned||to_owned(s)->T
        trait|core/str/traits/FromStr||from_str()->Result<T,E>
        trait|core/marker/Copy||
        trait|core/marker/Send||
        trait|core/marker/Sync||
        trait|core/marker/Sized||
        trait|core/ops/drop/Drop||drop(s)->()
        trait|core/ops/deref/Deref||deref(s)->T
        trait|core/ops/deref/DerefMut||deref_mut(s)->T
        trait|core/ops/arith/Add||add(s)->T
        trait|core/ops/arith/Sub||sub(s)->T
        trait|core/ops/arith/Mul||mul(s)->T
        trait|core/ops/arith/Div||div(s)->T
        trait|core/ops/arith/Rem||rem(s)->T
        trait|core/ops/arith/Neg||neg(s)->T
        trait|core/ops/arith/AddAssign||add_assign(s)->()
        trait|core/ops/arith/SubAssign||sub_assign(s)->()
        trait|core/ops/arith/MulAssign||mul_assign(s)->()
        trait|core/ops/arith/DivAssign||div_assign(s)->()
        trait|core/ops/arith/RemAssign||rem_assign(s)->()
        trait|core/ops/bit/Not||not(s)->T
        trait|core/ops/bit/BitAnd||bitand(s)->T
        trait|core/ops/bit/BitOr||bitor(s)->T
        trait|core/ops/bit/BitXor||bitxor(s)->T
        trait|core/ops/bit/Shl||shl(s)->T
        trait|core/ops/bit/Shr||shr(s)->T
        trait|core/ops/bit/BitAndAssign||bitand_assign(s)->()
        trait|core/ops/bit/BitOrAssign||bitor_assign(s)->()
        trait|core/ops/bit/BitXorAssign||bitxor_assign(s)->()
        trait|core/ops/bit/ShlAssign||shl_assign(s)->()
        trait|core/ops/bit/ShrAssign||shr_assign(s)->()
        trait|core/ops/index/Index||index(s)->T
        trait|core/ops/index/IndexMut||index_mut(s)->T
        trait|core/ops/function/Fn||call(s)->T
        trait|core/ops/function/FnMut||call_mut(s)->T
        trait|core/ops/function/FnOnce||call_once(s)->T
        trait|core/error/Error||source(s)->Option<T>;description(s)->str
        macro|std/macros/println
        macro|std/macros/print
        macro|std/macros/eprintln
        macro|std/macros/eprint
        macro|std/macros/dbg
        macro|alloc/macros/vec
        macro|alloc/macros/format
        macro|core/macros/assert_eq
        macro|core/macros/assert_ne
        macro|core/macros/debug_assert
        macro|core/macros/debug_assert_eq
        macro|core/macros/debug_assert_ne
        macro|core/macros/matches
        macro|core/macros/write
        macro|core/macros/writeln
        macro|core/macros/unreachable
        macro|core/macros/todo
        macro|core/macros/unimplemented
        macro|core/macros/builtin/assert
        macro|core/macros/builtin/panic
        alias|std/option|core/option
        alias|std/result|core/result
        alias|std/fmt|core/fmt
        alias|std/ops|core/ops
        alias|core/ops/Add|core/ops/arith/Add
        alias|core/ops/Sub|core/ops/arith/Sub
        alias|core/ops/Mul|core/ops/arith/Mul
        alias|core/ops/Div|core/ops/arith/Div
        alias|core/ops/Rem|core/ops/arith/Rem
        alias|core/ops/Neg|core/ops/arith/Neg
        alias|core/ops/AddAssign|core/ops/arith/AddAssign
        alias|core/ops/SubAssign|core/ops/arith/SubAssign
        alias|core/ops/MulAssign|core/ops/arith/MulAssign
        alias|core/ops/DivAssign|core/ops/arith/DivAssign
        alias|core/ops/RemAssign|core/ops/arith/RemAssign
        alias|core/ops/Not|core/ops/bit/Not
        alias|core/ops/BitAnd|core/ops/bit/BitAnd
        alias|core/ops/BitOr|core/ops/bit/BitOr
        alias|core/ops/BitXor|core/ops/bit/BitXor
        alias|core/ops/Shl|core/ops/bit/Shl
        alias|core/ops/Shr|core/ops/bit/Shr
        alias|core/ops/BitAndAssign|core/ops/bit/BitAndAssign
        alias|core/ops/BitOrAssign|core/ops/bit/BitOrAssign
        alias|core/ops/BitXorAssign|core/ops/bit/BitXorAssign
        alias|core/ops/ShlAssign|core/ops/bit/ShlAssign
        alias|core/ops/ShrAssign|core/ops/bit/ShrAssign
        alias|core/ops/Drop|core/ops/drop/Drop
        alias|core/ops/Deref|core/ops/deref/Deref
        alias|core/ops/DerefMut|core/ops/deref/DerefMut
        alias|core/ops/Index|core/ops/index/Index
        alias|core/ops/IndexMut|core/ops/index/IndexMut
        alias|core/ops/Fn|core/ops/function/Fn
        alias|core/ops/FnMut|core/ops/function/FnMut
        alias|core/ops/FnOnce|core/ops/function/FnOnce
        alias|core/iter/Iterator|core/iter/traits/iterator/Iterator
        alias|core/iter/IntoIterator|core/iter/traits/collect/IntoIterator
        alias|core/iter/FromIterator|core/iter/traits/collect/FromIterator
        alias|core/iter/Extend|core/iter/traits/collect/Extend
        alias|core/str/FromStr|core/str/traits/FromStr
        alias|std/cmp|core/cmp
        alias|std/clone|core/clone
        alias|std/default|core/default
        alias|std/convert|core/convert
        alias|std/iter|core/iter
        alias|std/hash|core/hash
        alias|std/marker|core/marker
        alias|std/str|core/str
        alias|std/cell|core/cell
        alias|std/slice|core/slice
        alias|std/error|core/error
        alias|std/time|core/time
        alias|std/vec|alloc/vec
        alias|std/string|alloc/string
        alias|std/boxed|alloc/boxed
        alias|std/rc|alloc/rc
        alias|std/borrow|alloc/borrow
        alias|std/sync/Arc|alloc/sync/Arc
        alias|std/collections/HashMap|std/collections/hash/map/HashMap
        alias|std/collections/HashSet|std/collections/hash/set/HashSet
        alias|std/collections/BTreeMap|alloc/collections/btree/map/BTreeMap
        alias|std/collections/BTreeSet|alloc/collections/btree/set/BTreeSet
        alias|std/collections/VecDeque|alloc/collections/vec_deque/VecDeque
        alias|std/collections/hash_map|std/collections/hash/map
        alias|std/collections/hash_set|std/collections/hash/set
        alias|std/collections/btree_map|alloc/collections/btree/map
        alias|core/fmt/Result|core/result/Result
        alias|std/prelude|core/prelude
        prelude|core/option/Option,core/option/Option/Some,core/option/Option/None,core/result/Result,core/result/Result/Ok,core/result/Result/Err,alloc/vec/Vec,alloc/string/String,alloc/boxed/Box,core/clone/Clone,core/default/Default,core/convert/From,core/convert/Into,core/convert/AsRef,core/convert/AsMut,core/convert/TryFrom,core/convert/TryInto,core/cmp/PartialEq,core/cmp/Eq,core/cmp/PartialOrd,core/cmp/Ord,core/iter/traits/iterator/Iterator,core/iter/traits/collect/IntoIterator,core/iter/traits/collect/FromIterator,core/iter/traits/collect/Extend,alloc/string/ToString,alloc/borrow/ToOwned,core/marker/Copy,core/marker/Send,core/marker/Sync,core/marker/Sized,core/ops/drop/Drop,core/ops/function/Fn,core/ops/function/FnMut,core/ops/function/FnOnce,std/macros/println,std/macros/print,std/macros/eprintln,std/macros/eprint,std/macros/dbg,alloc/macros/vec,alloc/macros/format,core/macros/assert_eq,core/macros/assert_ne,core/macros/debug_assert,core/macros/debug_assert_eq,core/macros/debug_assert_ne,core/macros/matches,core/macros/write,core/macros/writeln,core/macros/unreachable,core/macros/todo,core/macros/unimplemented,core/macros/builtin/assert,core/macros/builtin/panic
        """;
}
