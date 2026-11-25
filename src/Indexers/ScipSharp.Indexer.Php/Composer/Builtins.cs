namespace ScipSharp.Indexer.Php.Composer;

/// <summary>
/// Catalogue of PHP's built-in classes, functions and constants. Upstream scip-php takes this from the JetBrains
/// phpstorm-stubs package (<c>PhpStormStubsMap</c>), which is a PHP dependency and not available to a native
/// implementation. This is a hand-written subset covering the commonly used core / SPL / standard-library symbols; a
/// name missing from it is treated as an unknown symbol (no occurrence is emitted), exactly like an unresolvable name.
/// </summary>
internal static class Builtins
{
    private static HashSet<string> Split(string s) =>
        new(s.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    public static readonly HashSet<string> Classes = Split("""
        stdClass Traversable IteratorAggregate Iterator ArrayAccess Countable Serializable Stringable Throwable Closure Generator
        WeakReference WeakMap Attribute ReturnTypeWillChange AllowDynamicProperties SensitiveParameter Override UnitEnum BackedEnum
        IntBackedEnum StringBackedEnum Fiber FiberError InternalIterator JsonSerializable JsonException
        Exception ErrorException Error CompileError ParseError TypeError ValueError ArithmeticError DivisionByZeroError ArgumentCountError
        AssertionError UnhandledMatchError
        LogicException BadFunctionCallException BadMethodCallException DomainException InvalidArgumentException LengthException OutOfRangeException
        RuntimeException OutOfBoundsException OverflowException RangeException UnderflowException UnexpectedValueException
        ArrayObject ArrayIterator RecursiveArrayIterator AppendIterator CachingIterator CallbackFilterIterator EmptyIterator FilterIterator
        InfiniteIterator IteratorIterator LimitIterator MultipleIterator NoRewindIterator ParentIterator RecursiveCachingIterator
        RecursiveCallbackFilterIterator RecursiveDirectoryIterator RecursiveFilterIterator RecursiveIteratorIterator RecursiveRegexIterator
        RecursiveTreeIterator RegexIterator DirectoryIterator FilesystemIterator GlobIterator SplFileInfo SplFileObject SplTempFileObject
        SplDoublyLinkedList SplQueue SplStack SplHeap SplMinHeap SplMaxHeap SplPriorityQueue SplFixedArray SplObjectStorage SplObserver SplSubject
        OuterIterator RecursiveIterator SeekableIterator Countable
        DateTime DateTimeImmutable DateTimeInterface DateTimeZone DateInterval DatePeriod
        Reflector ReflectionException Reflection ReflectionClass ReflectionObject ReflectionMethod ReflectionFunction ReflectionFunctionAbstract
        ReflectionParameter ReflectionProperty ReflectionClassConstant ReflectionNamedType ReflectionType ReflectionUnionType
        ReflectionIntersectionType ReflectionEnum ReflectionEnumBackedCase ReflectionEnumUnitCase ReflectionExtension ReflectionGenerator
        ReflectionAttribute ReflectionReference ReflectionFiber
        PDO PDOException PDOStatement mysqli mysqli_result mysqli_stmt mysqli_sql_exception SQLite3 SQLite3Result SQLite3Stmt
        DOMDocument DOMElement DOMNode DOMNodeList DOMXPath DOMAttr DOMText DOMException SimpleXMLElement XMLReader XMLWriter
        Random\Randomizer Random\RandomException IntlDateFormatter NumberFormatter Collator Locale Normalizer Transliterator
        Directory php_user_filter SplEnum SplType SessionHandlerInterface SessionHandler
        Memcached Redis ZipArchive CURLFile CurlHandle CurlMultiHandle CURLStringFile finfo Phar PharData
        """);

    public static readonly HashSet<string> Functions = Split("""
        strlen count sizeof strcmp strcasecmp strncmp strncasecmp strnatcmp strnatcasecmp substr substr_count substr_replace str_replace str_ireplace
        str_pad str_repeat str_split str_word_count str_contains str_starts_with str_ends_with strpos stripos strrpos strripos strstr stristr strrchr
        strtolower strtoupper ucfirst ucwords lcfirst trim ltrim rtrim chop nl2br htmlspecialchars htmlspecialchars_decode html_entity_decode
        htmlentities addslashes stripslashes addcslashes stripcslashes quotemeta chr ord implode join explode sprintf printf vsprintf vprintf fprintf
        sscanf number_format money_format similar_text levenshtein soundex metaphone wordwrap strrev strtr strip_tags nl_langinfo md5 sha1 crc32 hash
        hash_hmac hash_algos hash_equals base64_encode base64_decode bin2hex hex2bin dechex hexdec bindec decbin decoct octdec base_convert urlencode
        urldecode rawurlencode rawurldecode http_build_query parse_url parse_str uniqid lcg_value random_int random_bytes mt_rand mt_srand rand srand
        mt_getrandmax getrandmax
        is_null is_int is_integer is_long is_float is_double is_string is_bool is_array is_object is_callable is_iterable is_countable is_numeric
        is_resource is_scalar is_a is_subclass_of intval floatval doubleval strval boolval settype gettype get_debug_type var_dump var_export print_r
        serialize unserialize json_encode json_decode json_last_error json_last_error_msg
        array_map array_filter array_reduce array_walk array_walk_recursive array_keys array_values array_merge array_merge_recursive array_replace
        array_replace_recursive array_combine array_flip array_slice array_splice array_search in_array array_key_exists key_exists array_key_first
        array_key_last array_unique array_reverse array_sum array_product array_push array_pop array_shift array_unshift array_fill array_fill_keys
        array_pad array_chunk array_column array_diff array_diff_key array_diff_assoc array_intersect array_intersect_key array_intersect_assoc
        array_udiff array_uintersect array_count_values array_rand array_is_list array_multisort range compact extract list each current key next
        prev reset end sort rsort usort uasort uksort asort arsort ksort krsort natsort natcasesort shuffle iterator_to_array iterator_count
        iterator_apply min max abs ceil floor round sqrt pow exp log log10 log2 sin cos tan asin acos atan atan2 pi fmod intdiv is_nan is_finite
        is_infinite hypot deg2rad rad2deg
        file_get_contents file_put_contents file fopen fclose fread fwrite fputs fgets fgetc feof fflush fseek ftell rewind ftruncate flock fscanf
        fgetcsv fputcsv fpassthru readfile file_exists is_file is_dir is_link is_readable is_writable is_writeable is_executable filesize filemtime
        fileatime filectime fileperms touch unlink rename copy mkdir rmdir scandir glob opendir readdir closedir rewinddir tempnam tmpfile
        sys_get_temp_dir realpath basename dirname pathinfo fnmatch getcwd chdir chmod chown stat lstat clearstatcache disk_free_space
        preg_match preg_match_all preg_replace preg_replace_callback preg_replace_callback_array preg_split preg_quote preg_grep preg_last_error
        preg_last_error_msg
        mb_strlen mb_substr mb_strtolower mb_strtoupper mb_strpos mb_strrpos mb_str_split mb_substr_count mb_convert_case mb_convert_encoding
        mb_detect_encoding mb_check_encoding mb_internal_encoding mb_strwidth mb_str_pad mb_strimwidth iconv iconv_strlen utf8_encode utf8_decode
        ctype_alnum ctype_alpha ctype_cntrl ctype_digit ctype_graph ctype_lower ctype_print ctype_punct ctype_space ctype_upper ctype_xdigit
        function_exists class_exists interface_exists trait_exists enum_exists method_exists property_exists get_class get_parent_class
        get_object_vars get_class_vars get_class_methods get_called_class class_implements class_uses spl_autoload_register spl_object_hash
        spl_object_id call_user_func call_user_func_array func_get_args func_get_arg func_num_args is_callable forward_static_call
        define defined constant ini_set ini_get set_error_handler restore_error_handler set_exception_handler restore_exception_handler
        trigger_error error_reporting error_log assert debug_backtrace debug_print_backtrace register_shutdown_function exit usleep sleep
        time microtime hrtime date gmdate mktime gmmktime strtotime checkdate date_default_timezone_get date_default_timezone_set date_create
        date_create_immutable date_diff idate getdate localtime strftime
        ob_start ob_get_clean ob_get_contents ob_end_clean ob_end_flush ob_get_level flush header headers_sent http_response_code setcookie
        session_start session_destroy session_id php_sapi_name phpversion php_uname php_ini_loaded_file memory_get_usage memory_get_peak_usage
        gc_collect_cycles getenv putenv gethostname version_compare extension_loaded get_loaded_extensions set_time_limit setlocale
        password_hash password_verify password_needs_rehash crypt openssl_encrypt openssl_decrypt openssl_random_pseudo_bytes
        gzcompress gzuncompress gzencode gzdecode gzdeflate gzinflate curl_init curl_setopt curl_exec curl_close curl_error
        array_flip array_any array_all array_find array_find_key str_word_count ucwords nl2br lcfirst similar_text array_map
        escapeshellarg escapeshellcmd exec shell_exec system passthru proc_open popen pclose mail filter_var filter_input ctype_alpha
        levenshtein is_writable spl_autoload_unregister iterator_to_array get_resource_type get_resource_id fdiv
        """);

    public static readonly HashSet<string> Constants = Split("""
        PHP_VERSION PHP_MAJOR_VERSION PHP_MINOR_VERSION PHP_RELEASE_VERSION PHP_VERSION_ID PHP_EXTRA_VERSION PHP_OS PHP_OS_FAMILY PHP_EOL PHP_INT_MAX
        PHP_INT_MIN PHP_INT_SIZE PHP_FLOAT_EPSILON PHP_FLOAT_MAX PHP_FLOAT_MIN PHP_FLOAT_DIG PHP_MAXPATHLEN DIRECTORY_SEPARATOR PATH_SEPARATOR
        PHP_SAPI PHP_BINARY PHP_PREFIX PHP_DEBUG PHP_ZTS NAN INF
        E_ERROR E_WARNING E_PARSE E_NOTICE E_CORE_ERROR E_CORE_WARNING E_COMPILE_ERROR E_COMPILE_WARNING E_USER_ERROR E_USER_WARNING E_USER_NOTICE
        E_STRICT E_RECOVERABLE_ERROR E_DEPRECATED E_USER_DEPRECATED E_ALL
        M_PI M_E M_LOG2E M_LOG10E M_LN2 M_LN10 M_PI_2 M_PI_4 M_1_PI M_2_PI M_SQRT2 M_SQRT1_2 M_EULER
        SORT_REGULAR SORT_NUMERIC SORT_STRING SORT_FLAG_CASE SORT_NATURAL SORT_ASC SORT_DESC COUNT_RECURSIVE COUNT_NORMAL
        ARRAY_FILTER_USE_KEY ARRAY_FILTER_USE_BOTH EXTR_OVERWRITE EXTR_SKIP EXTR_PREFIX_ALL
        JSON_PRETTY_PRINT JSON_UNESCAPED_SLASHES JSON_UNESCAPED_UNICODE JSON_THROW_ON_ERROR JSON_ERROR_NONE JSON_HEX_TAG JSON_HEX_QUOT JSON_HEX_AMP
        JSON_HEX_APOS JSON_NUMERIC_CHECK JSON_FORCE_OBJECT JSON_PARTIAL_OUTPUT_ON_ERROR JSON_PRESERVE_ZERO_FRACTION JSON_OBJECT_AS_ARRAY
        JSON_BIGINT_AS_STRING JSON_INVALID_UTF8_IGNORE JSON_INVALID_UTF8_SUBSTITUTE JSON_ERROR_SYNTAX
        LOCK_SH LOCK_EX LOCK_UN FILE_APPEND FILE_IGNORE_NEW_LINES FILE_SKIP_EMPTY_LINES FILE_USE_INCLUDE_PATH SEEK_SET SEEK_CUR SEEK_END
        STR_PAD_LEFT STR_PAD_RIGHT STR_PAD_BOTH ENT_QUOTES ENT_COMPAT ENT_NOQUOTES ENT_HTML5 ENT_HTML401 ENT_SUBSTITUTE
        PREG_SPLIT_NO_EMPTY PREG_SPLIT_DELIM_CAPTURE PREG_PATTERN_ORDER PREG_SET_ORDER PREG_OFFSET_CAPTURE PREG_UNMATCHED_AS_NULL
        LC_ALL LC_COLLATE LC_CTYPE LC_MONETARY LC_NUMERIC LC_TIME STDIN STDOUT STDERR
        PHP_ROUND_HALF_UP PHP_ROUND_HALF_DOWN PHP_ROUND_HALF_EVEN PHP_ROUND_HALF_ODD
        FILTER_VALIDATE_INT FILTER_VALIDATE_EMAIL FILTER_VALIDATE_URL FILTER_VALIDATE_BOOLEAN FILTER_VALIDATE_FLOAT FILTER_DEFAULT
        MB_CASE_UPPER MB_CASE_LOWER MB_CASE_TITLE DATE_ATOM DATE_RFC2822 DATE_ISO8601 DATE_RFC3339 DATE_COOKIE
        PASSWORD_DEFAULT PASSWORD_BCRYPT PASSWORD_ARGON2I PASSWORD_ARGON2ID
        __COMPILER_HALT_OFFSET__
        """);

    /// <summary>
    /// Parent classes / interfaces of built-in classes whose members we know about (see <see cref="Members"/>);
    /// used to find inherited members such as <c>Exception::getCode()</c>.
    /// </summary>
    public static readonly Dictionary<string, string[]> Parents = new()
    {
        ["Exception"] = ["Throwable", "Stringable"],
        ["Error"] = ["Throwable", "Stringable"],
        ["ErrorException"] = ["Exception"],
        ["CompileError"] = ["Error"],
        ["ParseError"] = ["CompileError"],
        ["TypeError"] = ["Error"],
        ["ValueError"] = ["Error"],
        ["ArithmeticError"] = ["Error"],
        ["DivisionByZeroError"] = ["ArithmeticError"],
        ["ArgumentCountError"] = ["TypeError"],
        ["AssertionError"] = ["Error"],
        ["UnhandledMatchError"] = ["Error"],
        ["LogicException"] = ["Exception"],
        ["BadFunctionCallException"] = ["LogicException"],
        ["BadMethodCallException"] = ["BadFunctionCallException"],
        ["DomainException"] = ["LogicException"],
        ["InvalidArgumentException"] = ["LogicException"],
        ["LengthException"] = ["LogicException"],
        ["OutOfRangeException"] = ["LogicException"],
        ["RuntimeException"] = ["Exception"],
        ["OutOfBoundsException"] = ["RuntimeException"],
        ["OverflowException"] = ["RuntimeException"],
        ["RangeException"] = ["RuntimeException"],
        ["UnderflowException"] = ["RuntimeException"],
        ["UnexpectedValueException"] = ["RuntimeException"],
        ["JsonException"] = ["Exception"],
        ["PDOException"] = ["RuntimeException"],
        ["ReflectionException"] = ["Exception"],
    };

    private static readonly string[] ThrowableMembers =
    [
        "__construct", "__wakeup", "getMessage", "getCode", "getPrevious", "getFile", "getLine", "getTrace", "getTraceAsString", "__toString",
    ];

    /// <summary>Member method names (return types are not tracked) of the built-in classes in <see cref="Parents"/>.</summary>
    public static IReadOnlyList<string> Members(string cls) => cls is "Exception" or "Error" ? ThrowableMembers : [];

    public static bool IsEnumInterface(string name) => name is "IntBackedEnum" or "StringBackedEnum";
}
