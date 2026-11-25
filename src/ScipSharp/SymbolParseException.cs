namespace ScipSharp;

/// <summary>Thrown by <see cref="SymbolParser.Parse"/> when a symbol string is malformed.</summary>
public sealed class SymbolParseException(string message) : FormatException(message);
