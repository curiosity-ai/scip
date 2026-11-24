using System.Buffers.Binary;
using System.Text;

namespace Scip.Indexer.Clang;

/// <summary>
/// wyhash "final version 4" (seed 0, default secret) as used by upstream scip-clang's <c>HashValue::forText</c>
/// to derive function disambiguators from the canonical type string.
/// </summary>
public static class WyHash
{
    private const ulong S0 = 0xa0761d6478bd642fUL;
    private const ulong S1 = 0xe7037ed1a0b428dbUL;
    private const ulong S2 = 0x8ebc6af09c88c6e3UL;
    private const ulong S3 = 0x589965cc75374cc3UL;

    private static ulong Mix(ulong a, ulong b)
    {
        var hi = Math.BigMul(a, b, out var lo);
        return lo ^ hi;
    }

    private static ulong R8(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt64LittleEndian(p);

    private static ulong R4(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt32LittleEndian(p);

    public static ulong Hash(ReadOnlySpan<byte> key, ulong seed = 0)
    {
        var len = key.Length;
        var p = key;
        seed ^= Mix(seed ^ S0, S1);
        ulong a, b;
        if (len <= 16)
        {
            if (len >= 4)
            {
                var o = (len >> 3) << 2;
                a = (R4(p) << 32) | R4(p[o..]);
                b = (R4(p[(len - 4)..]) << 32) | R4(p[(len - 4 - o)..]);
            }
            else if (len > 0)
            {
                a = ((ulong)p[0] << 16) | ((ulong)p[len >> 1] << 8) | p[len - 1];
                b = 0;
            }
            else
            {
                a = b = 0;
            }
        }
        else
        {
            var i = len;
            var off = 0;
            if (i > 48)
            {
                var see1 = seed;
                var see2 = seed;
                do
                {
                    seed = Mix(R8(key[off..]) ^ S1, R8(key[(off + 8)..]) ^ seed);
                    see1 = Mix(R8(key[(off + 16)..]) ^ S2, R8(key[(off + 24)..]) ^ see1);
                    see2 = Mix(R8(key[(off + 32)..]) ^ S3, R8(key[(off + 40)..]) ^ see2);
                    off += 48;
                    i -= 48;
                }
                while (i > 48);
                seed ^= see1 ^ see2;
            }

            while (i > 16)
            {
                seed = Mix(R8(key[off..]) ^ S1, R8(key[(off + 8)..]) ^ seed);
                i -= 16;
                off += 16;
            }

            a = R8(key[(off + i - 16)..]);
            b = R8(key[(off + i - 8)..]);
        }

        a ^= S1;
        b ^= seed;
        var hi = Math.BigMul(a, b, out var lo);
        return Mix(lo ^ S0 ^ (ulong)len, hi ^ S1);
    }

    /// <summary>Lower-case hex without leading zeros, like <c>fmt::format("{:x}")</c>.</summary>
    public static string HexOfText(string text) => Hash(Encoding.UTF8.GetBytes(text)).ToString("x", System.Globalization.CultureInfo.InvariantCulture);
}
