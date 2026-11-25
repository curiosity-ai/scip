using System.Text;

namespace ScipSharp.Indexer.Clang;

/// <summary>
/// A tiny, built-in model of the most used libc / libstdc++ headers. The indexer must not depend on a toolchain being
/// installed, so when <c>#include &lt;vector&gt;</c> cannot be found on the include path these declarations stand in for
/// it. They follow libstdc++'s spelling (namespaces, inline <c>__cxx11</c>, typedef names, member signatures) so symbols
/// and function disambiguators agree with what upstream produces for the real headers; the declarations live outside the
/// project root, so uses are reported as references to external symbols.
/// </summary>
public static class BuiltinHeaders
{
    /// <summary>Pseudo directory under which virtual headers live.</summary>
    public const string Prefix = "/__scip_builtin_headers__/";

    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["stddef.h"] = "cstddef", ["stdint.h"] = "cstdint", ["stdio.h"] = "cstdio", ["stdlib.h"] = "cstdlib", ["string.h"] = "cstring",
        ["assert.h"] = "cassert", ["math.h"] = "cmath", ["inttypes.h"] = "cstdint", ["stdbool.h"] = "cstdbool", ["limits.h"] = "climits",
        ["stdarg.h"] = "cstdarg", ["errno.h"] = "cerrno", ["time.h"] = "ctime",
    };

    private static readonly Dictionary<string, string> Sources = Build();

    public static string? Resolve(string name)
    {
        var key = Aliases.TryGetValue(name, out var a) ? a : name;
        return Sources.ContainsKey(key) ? Prefix + key : null;
    }

    public static bool IsVirtual(string path) => path.StartsWith(Prefix, StringComparison.Ordinal);

    public static byte[] Read(string path) => Encoding.UTF8.GetBytes(Sources.TryGetValue(path[Prefix.Length..], out var s) ? s : "");

    private static Dictionary<string, string> Build() => new()
    {
        ["cstddef"] = """
            #pragma once
            typedef unsigned long size_t;
            typedef long ptrdiff_t;
            typedef long double max_align_t;
            #define NULL 0
            namespace std {
              typedef unsigned long size_t;
              typedef long ptrdiff_t;
              typedef decltype(nullptr) nullptr_t;
              enum class byte : unsigned char {};
            }
            """,
        ["cstdint"] = """
            #pragma once
            typedef signed char int8_t;
            typedef unsigned char uint8_t;
            typedef short int16_t;
            typedef unsigned short uint16_t;
            typedef int int32_t;
            typedef unsigned int uint32_t;
            typedef long int64_t;
            typedef unsigned long uint64_t;
            typedef long intptr_t;
            typedef unsigned long uintptr_t;
            typedef long intmax_t;
            typedef unsigned long uintmax_t;
            namespace std {
              using ::int8_t; using ::uint8_t; using ::int16_t; using ::uint16_t; using ::int32_t; using ::uint32_t;
              using ::int64_t; using ::uint64_t; using ::intptr_t; using ::uintptr_t; using ::intmax_t; using ::uintmax_t;
            }
            """,
        ["cstdbool"] = "#pragma once\n",
        ["climits"] = "#pragma once\n#define INT_MAX 2147483647\n#define INT_MIN (-2147483647 - 1)\n#define UINT_MAX 4294967295U\n#define CHAR_BIT 8\n#define LONG_MAX 9223372036854775807L\n",
        ["cstdarg"] = "#pragma once\ntypedef __builtin_va_list va_list;\n",
        ["cerrno"] = "#pragma once\nextern int errno;\n",
        ["ctime"] = "#pragma once\ntypedef long time_t;\ntypedef long clock_t;\ntime_t time(time_t *);\nclock_t clock();\n",
        ["cassert"] = "#pragma once\n#define assert(x) ((void)0)\n",
        ["cmath"] = """
            #pragma once
            double sqrt(double);
            double pow(double, double);
            double sin(double);
            double cos(double);
            double floor(double);
            double ceil(double);
            double fabs(double);
            double exp(double);
            double log(double);
            namespace std {
              using ::sqrt; using ::pow; using ::sin; using ::cos; using ::floor; using ::ceil; using ::fabs; using ::exp; using ::log;
            }
            """,
        ["cstdio"] = """
            #pragma once
            #include <cstddef>
            struct _IO_FILE;
            typedef struct _IO_FILE FILE;
            extern FILE *stdin;
            extern FILE *stdout;
            extern FILE *stderr;
            int printf(const char *, ...);
            int fprintf(FILE *, const char *, ...);
            int sprintf(char *, const char *, ...);
            int snprintf(char *, size_t, const char *, ...);
            int puts(const char *);
            int putchar(int);
            int getchar();
            int fputs(const char *, FILE *);
            FILE *fopen(const char *, const char *);
            int fclose(FILE *);
            size_t fread(void *, size_t, size_t, FILE *);
            size_t fwrite(const void *, size_t, size_t, FILE *);
            int fflush(FILE *);
            int scanf(const char *, ...);
            void perror(const char *);
            namespace std {
              using ::FILE; using ::printf; using ::fprintf; using ::sprintf; using ::snprintf; using ::puts; using ::fputs;
              using ::fopen; using ::fclose; using ::fread; using ::fwrite; using ::fflush;
            }
            """,
        ["cstdlib"] = """
            #pragma once
            #include <cstddef>
            void *malloc(size_t);
            void *calloc(size_t, size_t);
            void *realloc(void *, size_t);
            void free(void *);
            void abort();
            void exit(int);
            int atoi(const char *);
            char *getenv(const char *);
            int abs(int);
            int rand();
            void srand(unsigned int);
            void qsort(void *, size_t, size_t, int (*)(const void *, const void *));
            namespace std {
              using ::malloc; using ::calloc; using ::realloc; using ::free; using ::abort; using ::exit; using ::atoi; using ::getenv;
              using ::abs; using ::rand; using ::srand; using ::qsort;
            }
            """,
        ["cstring"] = """
            #pragma once
            #include <cstddef>
            void *memcpy(void *, const void *, size_t);
            void *memmove(void *, const void *, size_t);
            void *memset(void *, int, size_t);
            int memcmp(const void *, const void *, size_t);
            size_t strlen(const char *);
            int strcmp(const char *, const char *);
            int strncmp(const char *, const char *, size_t);
            char *strcpy(char *, const char *);
            char *strncpy(char *, const char *, size_t);
            char *strcat(char *, const char *);
            char *strchr(const char *, int);
            char *strstr(const char *, const char *);
            namespace std {
              using ::memcpy; using ::memmove; using ::memset; using ::memcmp; using ::strlen; using ::strcmp; using ::strncmp;
              using ::strcpy; using ::strncpy; using ::strcat; using ::strchr; using ::strstr;
            }
            """,
        ["initializer_list"] = """
            #pragma once
            #include <cstddef>
            namespace std {
              template <class T>
              class initializer_list {
              public:
                typedef T value_type;
                typedef const T &reference;
                typedef const T &const_reference;
                typedef size_t size_type;
                typedef const T *iterator;
                typedef const T *const_iterator;
                constexpr initializer_list() noexcept {}
                constexpr size_t size() const noexcept { return 0; }
                constexpr const T *begin() const noexcept { return 0; }
                constexpr const T *end() const noexcept { return 0; }
              };
            }
            """,
        ["type_traits"] = """
            #pragma once
            namespace std {
              template <class T, T v>
              struct integral_constant {
                static constexpr T value = v;
                typedef T value_type;
                typedef integral_constant<T, v> type;
              };
              typedef integral_constant<bool, true> true_type;
              typedef integral_constant<bool, false> false_type;
              template <bool B, class T = void> struct enable_if {};
              template <class T> struct enable_if<true, T> { typedef T type; };
              template <class T, class U> struct is_same : false_type {};
              template <class T> struct is_same<T, T> : true_type {};
              template <class T> struct remove_reference { typedef T type; };
              template <class T> struct remove_reference<T &> { typedef T type; };
              template <class T> struct remove_reference<T &&> { typedef T type; };
              template <class T> struct remove_const { typedef T type; };
              template <class T> struct remove_const<const T> { typedef T type; };
              template <class T> struct is_integral : false_type {};
              template <class T> struct is_pointer : false_type {};
              template <class T> struct is_enum : false_type {};
              template <class T> struct decay { typedef T type; };
              template <bool B, class T = void> using enable_if_t = typename enable_if<B, T>::type;
            }
            """,
        ["utility"] = """
            #pragma once
            #include <type_traits>
            #include <initializer_list>
            namespace std {
              template <class T>
              constexpr typename remove_reference<T>::type &&move(T &&t) noexcept { return static_cast<typename remove_reference<T>::type &&>(t); }
              template <class T>
              constexpr T &&forward(typename remove_reference<T>::type &t) noexcept { return static_cast<T &&>(t); }
              template <class T> void swap(T &a, T &b) {}
              template <class T1, class T2>
              struct pair {
                typedef T1 first_type;
                typedef T2 second_type;
                T1 first;
                T2 second;
                constexpr pair() {}
                constexpr pair(const T1 &a, const T2 &b) {}
              };
              template <class T1, class T2> constexpr pair<T1, T2> make_pair(T1 a, T2 b) { return pair<T1, T2>(a, b); }
            }
            """,
        ["memory"] = """
            #pragma once
            #include <cstddef>
            #include <utility>
            namespace std {
              template <class T>
              class allocator {
              public:
                typedef T value_type;
                T *allocate(size_t n);
                void deallocate(T *p, size_t n);
              };
              template <class T>
              struct default_delete { void operator()(T *p) const {} };
              template <class T, class D = default_delete<T>>
              class unique_ptr {
              public:
                typedef T element_type;
                constexpr unique_ptr() noexcept {}
                explicit unique_ptr(T *p) noexcept {}
                T *get() const noexcept { return 0; }
                T *release() noexcept { return 0; }
                void reset(T *p = 0) noexcept {}
                T &operator*() const { return *get(); }
                T *operator->() const noexcept { return get(); }
                explicit operator bool() const noexcept { return true; }
              };
              template <class T>
              class shared_ptr {
              public:
                typedef T element_type;
                constexpr shared_ptr() noexcept {}
                explicit shared_ptr(T *p) {}
                T *get() const noexcept { return 0; }
                void reset() noexcept {}
                T &operator*() const noexcept { return *get(); }
                T *operator->() const noexcept { return get(); }
                explicit operator bool() const noexcept { return true; }
              };
              template <class T, class... Args> unique_ptr<T> make_unique(Args &&...args) { return unique_ptr<T>(); }
              template <class T, class... Args> shared_ptr<T> make_shared(Args &&...args) { return shared_ptr<T>(); }
            }
            """,
        ["string"] = """
            #pragma once
            #include <cstddef>
            #include <initializer_list>
            #include <memory>
            namespace std {
              template <class C> struct char_traits {};
              inline namespace __cxx11 {
                template <class C, class T = char_traits<C>, class A = allocator<C>>
                class basic_string {
                public:
                  typedef T traits_type;
                  typedef C value_type;
                  typedef A allocator_type;
                  typedef size_t size_type;
                  typedef C &reference;
                  typedef const C &const_reference;
                  typedef C *pointer;
                  typedef const C *const_pointer;
                  typedef C *iterator;
                  typedef const C *const_iterator;
                  static const size_type npos = -1;
                  basic_string() {}
                  basic_string(const C *s) {}
                  basic_string(const C *s, size_type n) {}
                  basic_string(const basic_string &str) {}
                  basic_string(basic_string &&str) noexcept {}
                  ~basic_string() {}
                  basic_string &operator=(const basic_string &str) { return *this; }
                  basic_string &operator=(const C *s) { return *this; }
                  size_type size() const noexcept { return 0; }
                  size_type length() const noexcept { return 0; }
                  bool empty() const noexcept { return true; }
                  const C *c_str() const noexcept { return 0; }
                  const C *data() const noexcept { return 0; }
                  void clear() noexcept {}
                  void push_back(C c) {}
                  basic_string &append(const C *s) { return *this; }
                  basic_string &append(const basic_string &str) { return *this; }
                  basic_string substr(size_type pos = 0, size_type n = npos) const { return *this; }
                  size_type find(const C *s, size_type pos = 0) const { return 0; }
                  size_type find(C c, size_type pos = 0) const noexcept { return 0; }
                  reference operator[](size_type pos) { return *(C *)0; }
                  const_reference operator[](size_type pos) const { return *(const C *)0; }
                  basic_string &operator+=(const basic_string &str) { return *this; }
                  basic_string &operator+=(const C *s) { return *this; }
                  iterator begin() noexcept { return 0; }
                  iterator end() noexcept { return 0; }
                  const_iterator begin() const noexcept { return 0; }
                  const_iterator end() const noexcept { return 0; }
                };
              }
              typedef basic_string<char> string;
              typedef basic_string<wchar_t> wstring;
            }
            """,
        ["vector"] = """
            #pragma once
            #include <cstddef>
            #include <initializer_list>
            #include <memory>
            namespace std {
              template <class T, class A = allocator<T>>
              class vector {
              public:
                typedef T value_type;
                typedef A allocator_type;
                typedef size_t size_type;
                typedef T &reference;
                typedef const T &const_reference;
                typedef T *pointer;
                typedef const T *const_pointer;
                typedef T *iterator;
                typedef const T *const_iterator;
                vector() noexcept {}
                explicit vector(size_type n) {}
                vector(size_type n, const value_type &value) {}
                vector(initializer_list<value_type> l) {}
                vector(const vector &x) {}
                vector(vector &&x) noexcept {}
                ~vector() {}
                vector &operator=(const vector &x) { return *this; }
                vector &operator=(vector &&x) noexcept { return *this; }
                iterator begin() noexcept { return 0; }
                iterator end() noexcept { return 0; }
                const_iterator begin() const noexcept { return 0; }
                const_iterator end() const noexcept { return 0; }
                size_type size() const noexcept { return 0; }
                bool empty() const noexcept { return true; }
                size_type capacity() const noexcept { return 0; }
                void reserve(size_type n) {}
                void resize(size_type n) {}
                void clear() noexcept {}
                reference operator[](size_type n) noexcept { return *(T *)0; }
                const_reference operator[](size_type n) const noexcept { return *(const T *)0; }
                reference at(size_type n) { return *(T *)0; }
                reference front() noexcept { return *(T *)0; }
                reference back() noexcept { return *(T *)0; }
                T *data() noexcept { return 0; }
                void push_back(const value_type &x) {}
                void push_back(value_type &&x) {}
                template <class... Args> reference emplace_back(Args &&...args) { return *(T *)0; }
                void pop_back() noexcept {}
                iterator erase(const_iterator position) { return 0; }
                iterator insert(const_iterator position, const value_type &x) { return 0; }
                void swap(vector &x) noexcept {}
              };
            }
            """,
        ["array"] = """
            #pragma once
            #include <cstddef>
            namespace std {
              template <class T, size_t N>
              struct array {
                typedef T value_type;
                typedef size_t size_type;
                typedef T &reference;
                typedef const T &const_reference;
                typedef T *iterator;
                typedef const T *const_iterator;
                T _M_elems[N];
                constexpr size_type size() const noexcept { return N; }
                constexpr reference operator[](size_type n) noexcept { return _M_elems[n]; }
                constexpr const_reference operator[](size_type n) const noexcept { return _M_elems[n]; }
                constexpr iterator begin() noexcept { return 0; }
                constexpr iterator end() noexcept { return 0; }
              };
            }
            """,
        ["map"] = """
            #pragma once
            #include <cstddef>
            #include <utility>
            #include <initializer_list>
            namespace std {
              template <class T> struct less {};
              template <class K, class V, class C = less<K>, class A = allocator<pair<const K, V>>>
              class map {
              public:
                typedef K key_type;
                typedef V mapped_type;
                typedef pair<const K, V> value_type;
                typedef size_t size_type;
                typedef value_type *iterator;
                typedef const value_type *const_iterator;
                map() {}
                map(initializer_list<value_type> l) {}
                V &operator[](const key_type &k) { return *(V *)0; }
                V &at(const key_type &k) { return *(V *)0; }
                iterator begin() noexcept { return 0; }
                iterator end() noexcept { return 0; }
                size_type size() const noexcept { return 0; }
                bool empty() const noexcept { return true; }
                iterator find(const key_type &k) { return 0; }
                size_type count(const key_type &k) const { return 0; }
                pair<iterator, bool> insert(const value_type &x) { return pair<iterator, bool>(); }
                size_type erase(const key_type &k) { return 0; }
                void clear() noexcept {}
              };
            }
            """,
        ["unordered_map"] = """
            #pragma once
            #include <cstddef>
            #include <utility>
            namespace std {
              template <class T> struct hash {};
              template <class K, class V, class H = hash<K>, class A = allocator<pair<const K, V>>>
              class unordered_map {
              public:
                typedef K key_type;
                typedef V mapped_type;
                typedef pair<const K, V> value_type;
                typedef size_t size_type;
                typedef value_type *iterator;
                unordered_map() {}
                V &operator[](const key_type &k) { return *(V *)0; }
                iterator begin() noexcept { return 0; }
                iterator end() noexcept { return 0; }
                size_type size() const noexcept { return 0; }
                bool empty() const noexcept { return true; }
                iterator find(const key_type &k) { return 0; }
                size_type count(const key_type &k) const { return 0; }
                size_type erase(const key_type &k) { return 0; }
                void clear() noexcept {}
              };
            }
            """,
        ["set"] = """
            #pragma once
            #include <cstddef>
            #include <utility>
            namespace std {
              template <class K, class C = less<K>, class A = allocator<K>>
              class set {
              public:
                typedef K key_type;
                typedef K value_type;
                typedef size_t size_type;
                typedef const K *iterator;
                set() {}
                iterator begin() const noexcept { return 0; }
                iterator end() const noexcept { return 0; }
                size_type size() const noexcept { return 0; }
                bool empty() const noexcept { return true; }
                iterator find(const key_type &k) const { return 0; }
                size_type count(const key_type &k) const { return 0; }
                pair<iterator, bool> insert(const value_type &x) { return pair<iterator, bool>(); }
                size_type erase(const key_type &k) { return 0; }
                void clear() noexcept {}
              };
            }
            """,
        ["optional"] = """
            #pragma once
            namespace std {
              struct nullopt_t {};
              template <class T>
              class optional {
              public:
                typedef T value_type;
                constexpr optional() noexcept {}
                constexpr optional(const T &v) {}
                constexpr bool has_value() const noexcept { return false; }
                constexpr T &value() & { return *(T *)0; }
                constexpr T &operator*() & noexcept { return *(T *)0; }
                constexpr T *operator->() noexcept { return 0; }
                constexpr explicit operator bool() const noexcept { return false; }
              };
            }
            """,
        ["functional"] = """
            #pragma once
            namespace std {
              template <class Sig> class function;
              template <class R, class... Args>
              class function<R(Args...)> {
              public:
                function() noexcept {}
                template <class F> function(F f) {}
                R operator()(Args... args) const { return R(); }
                explicit operator bool() const noexcept { return true; }
              };
            }
            """,
        ["algorithm"] = """
            #pragma once
            namespace std {
              template <class T> constexpr const T &min(const T &a, const T &b) { return a < b ? a : b; }
              template <class T> constexpr const T &max(const T &a, const T &b) { return a < b ? b : a; }
              template <class It> void sort(It first, It last) {}
              template <class It, class Cmp> void sort(It first, It last, Cmp comp) {}
              template <class It, class T> It find(It first, It last, const T &value) { return first; }
              template <class It, class F> F for_each(It first, It last, F f) { return f; }
              template <class It> void reverse(It first, It last) {}
            }
            """,
        ["iostream"] = """
            #pragma once
            #include <string>
            namespace std {
              class ios_base {};
              template <class C, class T = char_traits<C>>
              class basic_ostream : public ios_base {
              public:
                basic_ostream &operator<<(int n) { return *this; }
                basic_ostream &operator<<(long n) { return *this; }
                basic_ostream &operator<<(unsigned int n) { return *this; }
                basic_ostream &operator<<(double n) { return *this; }
                basic_ostream &operator<<(bool n) { return *this; }
                basic_ostream &operator<<(const void *p) { return *this; }
                basic_ostream &operator<<(basic_ostream &(*pf)(basic_ostream &)) { return *this; }
                basic_ostream &flush() { return *this; }
              };
              typedef basic_ostream<char> ostream;
              template <class C, class T>
              basic_ostream<C, T> &endl(basic_ostream<C, T> &os) { return os; }
              template <class T>
              basic_ostream<char> &operator<<(basic_ostream<char> &os, const char *s) { return os; }
              template <class C, class T = char_traits<C>>
              class basic_istream : public ios_base {
              public:
                basic_istream &operator>>(int &n) { return *this; }
                basic_istream &operator>>(double &n) { return *this; }
              };
              typedef basic_istream<char> istream;
              extern ostream cout;
              extern ostream cerr;
              extern istream cin;
            }
            """,
    };
}
