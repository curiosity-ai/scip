namespace ScipSharp.Indexer.Fortran.Semantics;

/// <summary>
/// Intrinsic procedure names (Fortran 2023 plus the FORTRAN 77 specific names and common vendor extensions). An
/// undeclared name that matches is treated as the intrinsic rather than as an external procedure.
/// </summary>
internal static class Intrinsics
{
    private static readonly HashSet<string> Functions = new(StringComparer.Ordinal)
    {
        // Fortran 2023 generic intrinsic functions
        "abs", "achar", "acos", "acosd", "acosh", "acospi", "adjustl", "adjustr", "aimag", "aint", "all", "allocated", "anint", "any",
        "asin", "asind", "asinh", "asinpi", "associated", "atan", "atan2", "atan2d", "atan2pi", "atand", "atanh", "atanpi",
        "bessel_j0", "bessel_j1", "bessel_jn", "bessel_y0", "bessel_y1", "bessel_yn", "bge", "bgt", "bit_size", "ble", "blt",
        "btest", "ceiling", "char", "cmplx", "command_argument_count", "conjg", "cos", "cosd", "cosh", "cospi", "count",
        "cshift", "dble", "digits", "dim", "dot_product", "dprod", "dshiftl", "dshiftr", "eoshift", "epsilon", "erf", "erfc",
        "erfc_scaled", "exp", "exponent", "extends_type_of", "findloc", "floor", "fraction", "gamma", "huge", "hypot",
        "iachar", "iall", "iand", "iany", "ibclr", "ibits", "ibset", "ichar", "ieor", "image_index", "index", "int", "ior",
        "iparity", "ishft", "ishftc", "is_contiguous", "is_iostat_end", "is_iostat_eor", "kind", "lbound", "lcobound",
        "leadz", "len", "len_trim", "lge", "lgt", "lle", "llt", "log", "log10", "log_gamma", "logical", "maskl", "maskr",
        "matmul", "max", "maxexponent", "maxloc", "maxval", "merge", "merge_bits", "min", "minexponent", "minloc", "minval",
        "mod", "modulo", "nearest", "new_line", "nint", "norm2", "not", "null", "num_images", "out_of_range", "pack",
        "parity", "popcnt", "poppar", "precision", "present", "product", "radix", "range", "rank", "real", "reduce",
        "repeat", "reshape", "rrspacing", "same_type_as", "scale", "scan", "selected_char_kind", "selected_int_kind",
        "selected_logical_kind", "selected_real_kind", "set_exponent", "shape", "shifta", "shiftl", "shiftr", "sign", "sin",
        "sind", "sinh", "sinpi", "size", "spacing", "spread", "sqrt", "storage_size", "sum", "tan", "tand", "tanh", "tanpi",
        "this_image", "tiny", "trailz", "transfer", "transpose", "trim", "ubound", "ucobound", "unpack", "verify",
        "coshape", "failed_images", "stopped_images", "image_status", "team_number", "get_team",
        "c_loc", "c_funloc", "c_associated", "c_sizeof",

        // FORTRAN 77 specific names
        "alog", "alog10", "amax0", "amax1", "amin0", "amin1", "amod", "cabs", "ccos", "cexp", "clog", "csin", "csqrt",
        "dabs", "dacos", "dasin", "datan", "datan2", "dcos", "dcosh", "ddim", "dexp", "dint", "dlog", "dlog10", "dmax1",
        "dmin1", "dmod", "dnint", "dsign", "dsin", "dsinh", "dsqrt", "dtan", "dtanh", "float", "iabs", "idim", "idint",
        "idnint", "ifix", "isign", "max0", "max1", "min0", "min1", "sngl", "dcmplx", "dconjg", "dimag", "dreal", "cdabs",
        "cdexp", "cdlog", "cdsqrt", "cdsin", "cdcos", "dfloat", "zabs", "zexp", "zlog", "zsqrt", "imag", "iiabs", "jiabs",

        // common vendor extensions (GNU, Intel, DEC)
        "loc", "sizeof", "iargc", "getarg", "rand", "irand", "ran", "srand", "etime", "dtime", "secnds", "second", "lnblnk",
        "isnan", "and", "or", "xor", "lshift", "rshift", "getcwd", "getenv", "getpid", "hostnm", "ierrno", "isatty",
        "malloc", "time", "time8", "ctime", "fdate", "dfloat", "dreal", "cotan", "cotand", "int2", "int8", "fnum", "ftell",
    };

    private static readonly HashSet<string> Subroutines = new(StringComparer.Ordinal)
    {
        "cpu_time", "date_and_time", "event_query", "execute_command_line", "get_command", "get_command_argument",
        "get_environment_variable", "move_alloc", "mvbits", "random_init", "random_number", "random_seed", "system_clock",
        "co_broadcast", "co_max", "co_min", "co_reduce", "co_sum", "atomic_add", "atomic_and", "atomic_cas", "atomic_define",
        "atomic_fetch_add", "atomic_fetch_and", "atomic_fetch_or", "atomic_fetch_xor", "atomic_or", "atomic_ref",
        "atomic_xor", "split", "tokenize", "c_f_pointer", "c_f_procpointer", "c_f_strpointer",
        "abort", "exit", "flush", "getarg", "getlog", "sleep", "system", "date", "idate", "itime", "ltime", "gmtime",
        "chdir", "fseek", "perror", "free", "signal", "srand", "backtrace", "dtime", "etime", "fdate", "getenv", "getcwd",
    };

    public static bool IsFunction(string name) => Functions.Contains(name);

    public static bool IsSubroutine(string name) => Subroutines.Contains(name) || Functions.Contains(name);
}
