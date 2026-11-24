namespace Scip.Indexer.Python;

/// <summary>Static knowledge that scip-python hardcodes (port of <c>hardcoded.ts</c>) plus builtin classification.</summary>
internal static class Hardcoded
{
    public static readonly HashSet<string> StdlibModuleNames = new(StringComparer.Ordinal)
    {
        "__future__", "_abc", "_aix_support", "_ast", "_asyncio", "_bisect",
        "_blake2", "_bootsubprocess", "_bz2", "_codecs", "_codecs_cn", "_codecs_hk",
        "_codecs_iso2022", "_codecs_jp", "_codecs_kr", "_codecs_tw", "_collections", "_collections_abc",
        "_compat_pickle", "_compression", "_contextvars", "_crypt", "_csv", "_ctypes",
        "_curses", "_curses_panel", "_datetime", "_dbm", "_decimal", "_elementtree",
        "_frozen_importlib", "_frozen_importlib_external", "_functools", "_gdbm", "_hashlib", "_heapq",
        "_imp", "_io", "_json", "_locale", "_lsprof", "_lzma",
        "_markupbase", "_md5", "_msi", "_multibytecodec", "_multiprocessing", "_opcode",
        "_operator", "_osx_support", "_overlapped", "_pickle", "_posixshmem", "_posixsubprocess",
        "_py_abc", "_pydecimal", "_pyio", "_queue", "_random", "_scproxy",
        "_sha1", "_sha256", "_sha3", "_sha512", "_signal", "_sitebuiltins",
        "_socket", "_sqlite3", "_sre", "_ssl", "_stat", "_statistics",
        "_string", "_strptime", "_struct", "_symtable", "_thread", "_threading_local",
        "_tkinter", "_tracemalloc", "_uuid", "_warnings", "_weakref", "_weakrefset",
        "_winapi", "_zoneinfo", "abc", "aifc", "antigravity", "argparse",
        "array", "ast", "asynchat", "asyncio", "asyncore", "atexit",
        "audioop", "base64", "bdb", "binascii", "binhex", "bisect",
        "builtins", "bz2", "cProfile", "calendar", "cgi", "cgitb",
        "chunk", "cmath", "cmd", "code", "codecs", "codeop",
        "collections", "colorsys", "compileall", "concurrent", "configparser", "contextlib",
        "contextvars", "copy", "copyreg", "crypt", "csv", "ctypes",
        "curses", "dataclasses", "datetime", "dbm", "decimal", "difflib",
        "dis", "distutils", "doctest", "email", "encodings", "ensurepip",
        "enum", "errno", "faulthandler", "fcntl", "filecmp", "fileinput",
        "fnmatch", "fractions", "ftplib", "functools", "gc", "genericpath",
        "getopt", "getpass", "gettext", "glob", "graphlib", "grp",
        "gzip", "hashlib", "heapq", "hmac", "html", "http",
        "idlelib", "imaplib", "imghdr", "imp", "importlib", "inspect",
        "io", "ipaddress", "itertools", "json", "keyword", "lib2to3",
        "linecache", "locale", "logging", "lzma", "mailbox", "mailcap",
        "marshal", "math", "mimetypes", "mmap", "modulefinder", "msilib",
        "msvcrt", "multiprocessing", "netrc", "nis", "nntplib", "nt",
        "ntpath", "nturl2path", "numbers", "opcode", "operator", "optparse",
        "os", "ossaudiodev", "pathlib", "pdb", "pickle", "pickletools",
        "pipes", "pkgutil", "platform", "plistlib", "poplib", "posix",
        "posixpath", "pprint", "profile", "pstats", "pty", "pwd",
        "py_compile", "pyclbr", "pydoc", "pydoc_data", "pyexpat", "queue",
        "quopri", "random", "re", "readline", "reprlib", "resource",
        "rlcompleter", "runpy", "sched", "secrets", "select", "selectors",
        "shelve", "shlex", "shutil", "signal", "site", "smtpd",
        "smtplib", "sndhdr", "socket", "socketserver", "spwd", "sqlite3",
        "sre_compile", "sre_constants", "sre_parse", "ssl", "stat", "statistics",
        "string", "stringprep", "struct", "subprocess", "sunau", "symtable",
        "sys", "sysconfig", "syslog", "tabnanny", "tarfile", "telnetlib",
        "tempfile", "termios", "textwrap", "this", "threading", "time",
        "timeit", "tkinter", "token", "tokenize", "trace", "traceback",
        "tracemalloc", "tty", "turtle", "turtledemo", "types", "typing",
        "unicodedata", "unittest", "urllib", "uu", "uuid", "venv",
        "warnings", "wave", "weakref", "webbrowser", "winreg", "winsound",
        "wsgiref", "xdrlib", "xml", "xmlrpc", "zipapp", "zipfile",
        "zipimport", "zlib", "zoneinfo",
    };

    /// <summary>Classes of <c>builtins.pyi</c> (including exceptions); these get <c>builtins/Name#</c> symbols.</summary>
    private static readonly HashSet<string> BuiltinClasses = new(StringComparer.Ordinal)
    {
        "object", "type", "int", "float", "complex", "bool", "str", "bytes", "bytearray", "memoryview", "list", "dict", "set",
        "frozenset", "tuple", "range", "slice", "property", "staticmethod", "classmethod", "super", "enumerate", "zip", "map",
        "filter", "reversed", "function", "ellipsis", "NotImplemented", "Ellipsis",
        "BaseException", "Exception", "ArithmeticError", "AssertionError", "AttributeError", "BlockingIOError",
        "BrokenPipeError", "BufferError", "BytesWarning", "ChildProcessError", "ConnectionAbortedError", "ConnectionError",
        "ConnectionRefusedError", "ConnectionResetError", "DeprecationWarning", "EOFError", "EnvironmentError",
        "FileExistsError", "FileNotFoundError", "FloatingPointError", "FutureWarning", "GeneratorExit", "IOError",
        "ImportError", "ImportWarning", "IndentationError", "IndexError", "InterruptedError", "IsADirectoryError", "KeyError",
        "KeyboardInterrupt", "LookupError", "MemoryError", "ModuleNotFoundError", "NameError", "NotADirectoryError",
        "NotImplementedError", "OSError", "OverflowError", "PendingDeprecationWarning", "PermissionError",
        "ProcessLookupError", "RecursionError", "ReferenceError", "ResourceWarning", "RuntimeError", "RuntimeWarning",
        "StopAsyncIteration", "StopIteration", "SyntaxError", "SyntaxWarning", "SystemError", "SystemExit", "TabError",
        "TimeoutError", "TypeError", "UnboundLocalError", "UnicodeDecodeError", "UnicodeEncodeError", "UnicodeError",
        "UnicodeTranslateError", "UnicodeWarning", "UserWarning", "ValueError", "Warning", "ZeroDivisionError",
        "BaseExceptionGroup", "ExceptionGroup", "EncodingWarning", "PythonFinalizationError",
        // module attributes that typeshed types as instances of builtin classes; scip-python emits them as `builtins/name#`
        "__name__", "__file__", "__doc__", "__package__",
    };

    /// <summary>Builtin functions that typeshed declares with <c>@overload</c> (pyright reports those as OverloadedFunction).</summary>
    public static readonly HashSet<string> OverloadedBuiltinFunctions = new(StringComparer.Ordinal)
    {
        "print", "iter", "next", "sum", "max", "min", "round", "pow", "open", "getattr", "sorted", "compile", "aiter", "anext",
    };

    private static readonly HashSet<string> BuiltinFunctions = new(StringComparer.Ordinal)
    {
        "abs", "aiter", "all", "anext", "any", "ascii", "bin", "breakpoint", "callable", "chr", "compile", "copyright", "credits",
        "delattr", "dir", "divmod", "eval", "exec", "exit", "format", "getattr", "globals", "hasattr", "hash", "help", "hex", "id",
        "input", "isinstance", "issubclass", "iter", "len", "license", "locals", "max", "min", "next", "oct", "open", "ord", "pow",
        "print", "quit", "repr", "round", "setattr", "sorted", "sum", "vars", "__import__", "__build_class__", "__debug__",
        "__spec__", "__loader__", "__path__", "__builtins__", "__dict__",
    };

    /// <summary>Implicit module attributes pyright's binder declares (<c>DeclarationType.Intrinsic</c>).</summary>
    private static readonly HashSet<string> ModuleIntrinsics = new(StringComparer.Ordinal)
    {
        "__doc__", "__name__", "__qualname__", "__loader__", "__package__", "__spec__", "__path__", "__file__", "__cached__",
        "__dict__", "__annotations__", "__builtins__",
    };

    public static bool IsModuleIntrinsic(string name) => ModuleIntrinsics.Contains(name);

    private static readonly HashSet<string> BuiltinsImportedClasses = new(StringComparer.Ordinal)
    {
        "IO", "BinaryIO", "ByteString", "ClassVar", "Generic", "Mapping", "MutableMapping", "MutableSequence", "NoReturn", "Protocol",
        "Sequence", "SupportsAbs", "SupportsBytes", "SupportsComplex", "SupportsFloat", "SupportsInt", "TypeVar", "Concatenate",
        "Literal", "LiteralString", "ParamSpec", "Self", "SupportsIndex", "TypeAlias", "TypeGuard", "Awaitable", "Callable",
        "Iterable", "Iterator", "MutableSet", "Reversible", "Sized",
    };

    /// <summary>Names that typeshed's <c>builtins.pyi</c> imports from typing, typing_extensions and collections.abc.</summary>
    public static bool IsBuiltinsImportedClass(string module, string name) =>
        module is "typing" or "typing_extensions" or "collections.abc" or "_collections_abc" && BuiltinsImportedClasses.Contains(name);

    private static readonly Dictionary<string, string[]> LowercaseClasses = new(StringComparer.Ordinal)
    {
        ["datetime"] = ["tzinfo", "timezone", "date", "time", "timedelta", "datetime"],
        ["collections"] = ["deque", "defaultdict"],
        ["itertools"] = ["count", "cycle", "repeat", "accumulate", "chain", "compress", "dropwhile", "filterfalse", "groupby", "islice",
            "starmap", "takewhile", "zip_longest", "product", "permutations", "combinations", "combinations_with_replacement", "pairwise", "batched"],
        ["functools"] = ["partial", "partialmethod", "singledispatchmethod", "cached_property"],
        ["contextlib"] = ["closing", "aclosing", "suppress", "redirect_stdout", "redirect_stderr", "nullcontext", "chdir"],
        ["threading"] = ["local"],
        ["weakref"] = ["finalize"],
        ["warnings"] = ["catch_warnings"],
        ["time"] = ["struct_time"],
        ["os"] = ["stat_result", "statvfs_result", "uname_result", "terminal_size", "times_result", "waitid_result", "sched_param"],
        ["signal"] = ["struct_siginfo"],
    };

    /// <summary>Lowercase class names of stdlib stubs (classes that are not CamelCase).</summary>
    public static bool IsStdlibLowercaseClass(string module, string name) =>
        LowercaseClasses.TryGetValue(module, out var names) && names.Contains(name);

    private static readonly Dictionary<string, string[]> AnnotatedVariables = new(StringComparer.Ordinal)
    {
        ["os"] = ["environ", "environb", "sep", "altsep", "extsep", "pathsep", "linesep", "curdir", "pardir", "devnull", "name",
            "supports_bytes_environ", "supports_dir_fd", "supports_fd", "supports_effective_ids", "supports_follow_symlinks", "defpath"],
        ["sys"] = ["abiflags", "argv", "base_exec_prefix", "base_prefix", "byteorder", "builtin_module_names", "copyright",
            "dont_write_bytecode", "displayhook", "excepthook", "exec_prefix", "executable", "float_repr_style", "hexversion",
            "maxsize", "maxunicode", "meta_path", "modules", "orig_argv", "path", "path_hooks", "path_importer_cache", "platform",
            "platlibdir", "prefix", "pycache_prefix", "stdin", "stdout", "stderr", "version", "version_info", "flags", "float_info",
            "implementation", "api_version", "warnoptions"],
        ["logging"] = ["raiseExceptions", "logThreads", "logMultiprocessing", "logProcesses", "lastResort", "root", "BASIC_FORMAT"],
        ["warnings"] = ["filters"],
        ["time"] = ["altzone", "daylight", "timezone", "tzname"],
    };

    /// <summary>Stdlib variables that stubs declare with a type annotation.</summary>
    public static bool IsStdlibAnnotatedVariable(string module, string name) =>
        AnnotatedVariables.TryGetValue(module, out var names) && names.Contains(name);

    public static bool IsBuiltinClass(string name) => BuiltinClasses.Contains(name);

    public static bool IsBuiltin(string name) => BuiltinClasses.Contains(name) || BuiltinFunctions.Contains(name);

    /// <summary>Heuristic used when no stub is available: CamelCase names are classes.</summary>
    public static bool LooksLikeClass(string name) =>
        name.Length > 0 && char.IsUpper(name[0]) && name.Any(char.IsLower);

    /// <summary>Dotted standard-library submodules that are commonly reached via attribute access on the parent package.</summary>
    private static readonly HashSet<string> StdlibSubmodules = new(StringComparer.Ordinal)
    {
        "os.path", "collections.abc", "importlib.abc", "importlib.machinery", "importlib.metadata", "importlib.resources",
        "importlib.util", "concurrent.futures", "logging.handlers", "logging.config", "urllib.request", "urllib.parse",
        "urllib.error", "urllib.response", "http.client", "http.server", "http.cookies", "http.cookiejar", "unittest.mock",
        "json.decoder", "json.encoder", "multiprocessing.pool", "multiprocessing.managers", "xml.etree", "xml.dom", "xml.sax",
        "email.mime", "email.message", "email.utils", "ctypes.util", "ctypes.wintypes", "asyncio.subprocess", "asyncio.events",
        "asyncio.tasks", "asyncio.queues", "os.sys", "xml.etree.ElementTree", "wsgiref.simple_server", "sqlite3.dbapi2",
        "tkinter.ttk", "tkinter.messagebox", "tkinter.filedialog", "distutils.core", "encodings.utf_8", "curses.ascii",
    };

    public static bool IsStdlibSubmodule(string dotted) => StdlibSubmodules.Contains(dotted);

    private static readonly HashSet<string> TypingSpecialForms = new(StringComparer.Ordinal)
    {
        "Any", "Callable", "Optional", "Union", "List", "Dict", "Set", "FrozenSet", "Tuple", "Type", "Generic", "Protocol",
        "ClassVar", "Final", "Literal", "Annotated", "Concatenate", "TypeAlias", "Deque", "DefaultDict", "OrderedDict",
        "Counter", "ChainMap", "Text", "NoReturn", "Never", "Self", "LiteralString", "TypeGuard", "Required", "NotRequired",
        "Unpack", "ReadOnly", "TypeIs",
    };

    /// <summary>typeshed declares these as variables (special forms), so they get term (<c>.</c>) symbols.</summary>
    public static bool IsTypingSpecialForm(string module, string name) =>
        module is "typing" or "typing_extensions" && TypingSpecialForms.Contains(name);
}
