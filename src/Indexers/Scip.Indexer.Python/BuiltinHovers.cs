namespace Scip.Indexer.Python;

/// <summary>Hover signatures pyright prints for non-overloaded functions of typeshed's <c>builtins.pyi</c> (generated from the stub).</summary>
internal static class BuiltinHovers
{
    public static readonly Dictionary<string, string> Functions = new(StringComparer.Ordinal)
    {
        ["all"] = "(function) def all(\n    __iterable: Iterable[object],\n    /\n) -> bool",
        ["any"] = "(function) def any(\n    __iterable: Iterable[object],\n    /\n) -> bool",
        ["ascii"] = "(function) def ascii(\n    __obj: object,\n    /\n) -> str",
        ["bin"] = "(function) def bin(\n    __number: int | SupportsIndex,\n    /\n) -> str",
        ["breakpoint"] = "(function) def breakpoint(\n    *args: Any,\n    **kws: Any\n) -> None",
        ["chr"] = "(function) def chr(\n    __i: int,\n    /\n) -> str",
        ["copyright"] = "(function) def copyright() -> None",
        ["credits"] = "(function) def credits() -> None",
        ["delattr"] = "(function) def delattr(\n    __obj: object,\n    __name: str,\n    /\n) -> None",
        ["dir"] = "(function) def dir(\n    __o: object = ...,\n    /\n) -> list[str]",
        ["eval"] = "(function) def eval(\n    __source: str | ReadableBuffer | CodeType,\n    __globals: dict[str, Any] | None = None,\n    __locals: Mapping[str, object] | None = None,\n    /\n) -> Any",
        ["format"] = "(function) def format(\n    __value: object,\n    __format_spec: str = '',\n    /\n) -> str",
        ["globals"] = "(function) def globals() -> dict[str, Any]",
        ["hasattr"] = "(function) def hasattr(\n    __obj: object,\n    __name: str,\n    /\n) -> bool",
        ["hash"] = "(function) def hash(\n    __obj: object,\n    /\n) -> int",
        ["help"] = "(function) def help(request: object = ...) -> None",
        ["hex"] = "(function) def hex(\n    __number: int | SupportsIndex,\n    /\n) -> str",
        ["id"] = "(function) def id(\n    __obj: object,\n    /\n) -> int",
        ["input"] = "(function) def input(\n    __prompt: object = '',\n    /\n) -> str",
        ["isinstance"] = "(function) def isinstance(\n    __obj: object,\n    __class_or_tuple: _ClassInfo,\n    /\n) -> bool",
        ["issubclass"] = "(function) def issubclass(\n    __cls: type,\n    __class_or_tuple: _ClassInfo,\n    /\n) -> bool",
        ["len"] = "(function) def len(\n    __obj: Sized,\n    /\n) -> int",
        ["license"] = "(function) def license() -> None",
        ["locals"] = "(function) def locals() -> dict[str, Any]",
        ["oct"] = "(function) def oct(\n    __number: int | SupportsIndex,\n    /\n) -> str",
        ["ord"] = "(function) def ord(\n    __c: str | bytes | bytearray,\n    /\n) -> int",
        ["repr"] = "(function) def repr(\n    __obj: object,\n    /\n) -> str",
        ["setattr"] = "(function) def setattr(\n    __obj: object,\n    __name: str,\n    __value: Any,\n    /\n) -> None",
    };

    /// <summary>Hover of <c>typing.overload</c>: a function that <c>builtins.pyi</c> imports, so pyright treats it as a builtin.</summary>
    public static readonly string[] Overload =
    [
        "```python\n(function) def overload(func: _F@overload) -> _F@overload\n```",
        "---\n",
        "Decorator for overloaded functions/methods.\n\nIn a non-stub file, place two or more stub definitions for the same\nfunction in a row, each decorated with @overload, followed\nby an implementation.  The implementation should \\*not\\*\nbe decorated with @overload:\n\n```\n    @overload\n    def utf8(value: None) -> None: ...\n    @overload\n    def utf8(value: bytes) -> bytes: ...\n    @overload\n    def utf8(value: str) -> bytes: ...\n    def utf8(value):\n        ...  # implementation goes here\n```\n\nIn a stub file or in an abstract method (for example, in a Protocol definition),\nthe implementation may be omitted:\n\n```\n    @overload\n    def utf8(value: None) -> None: ...\n    @overload\n    def utf8(value: bytes) -> bytes: ...\n    @overload\n    def utf8(value: str) -> bytes: ...\n```\n\nThe overloads for a function can be retrieved at runtime using the\nget\\_overloads() function.",
    ];
}
