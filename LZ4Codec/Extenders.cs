using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LZ4;

public static class ExtendersPublic
{
    public static (int UnpackedLength, int PackedLength) LZ4GetLengths(this Span<byte> span) =>
        (span.LZ4UnpackedLength(),
         span.LZ4PackedLength());
}

static class Extenders
{
    internal static int LZ4UnpackedLength(this Span<byte> span) =>
        span.Length < 8 ? 0 : BitConverter.ToInt32(span);

    internal static int LZ4PackedLength(this Span<byte> span) =>
        span.Length < 8 ? 0 : BitConverter.ToInt32(span.Slice(4));

    #region Peek2 / Peek4

    internal static unsafe ushort Peek2(this Span<byte> span, int offset)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            return *(ushort*) (ptr + offset);
    }

    internal static unsafe uint Peek4(this Span<byte> span, int offs)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            return *(uint*) (ptr + offs);
    }

    #endregion

    #region Equal2 / Equal4

    internal static unsafe bool Equal2(this Span<byte> span, int offset1, int offset2)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            return *(ushort*) (ptr + offset1) == *(ushort*) (ptr + offset2);
    }

    internal static unsafe bool Equal4(this Span<byte> span, int offset1, int offset2)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            return *(uint*) (ptr + offset1) == *(uint*) (ptr + offset2);
    }

    #endregion

    internal static unsafe void Poke2(this Span<byte> span, int offset, ushort value)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            *(ushort*) (ptr + offset) = value;
    }

    internal static unsafe int WildCopy(this Span<byte> src, int srcOffset, Span<byte> dst, int dstOffset, int dstOffsetEnd)
    {
        var len = dstOffsetEnd - dstOffset;

        fixed (byte* ptrFrom = &MemoryMarshal.GetReference(src))
        fixed (byte* ptrTo = &MemoryMarshal.GetReference(dst))
            Buffer.MemoryCopy(ptrFrom + srcOffset, ptrTo + dstOffset, len, len);

        return len;
    }

    internal static unsafe int SecureCopy(this Span<byte> span, int src, int dst, int dst_end)
    {
        var diff   = dst     - src;
        var length = dst_end - dst;
        var len    = length;

        if (diff >= 16)
        {
            if (diff >= length)
            {
                fixed (byte* ptr = &MemoryMarshal.GetReference(span))
                    Buffer.MemoryCopy(ptr + src, ptr + dst, length, length);
                return length;
            }

            do
            {
                fixed (byte* ptr = &MemoryMarshal.GetReference(span))
                    Buffer.MemoryCopy(ptr + src, ptr + dst, diff, diff);

                src += diff;
                dst += diff;
                len -= diff;
            } while (len >= diff);
        }

        while (len >= 4)
        {
            fixed (byte* ptrFrom = &span[src], ptrTo = &span[dst])
                *(uint*) ptrTo = *(uint*) ptrFrom;

            dst += 4;
            src += 4;
            len -= 4;
        }

        while (len-- > 0)
            span[dst++] = span[src++];

        return length;
    }

    #region Copy4 / Copy8

    internal static unsafe void Copy4(this Span<byte> span, int src, int dst)
    {
        fixed (byte* ptrFrom = &span[src], ptrTo = &span[dst])
            *(uint*) ptrTo = *(uint*) ptrFrom;
    }

    internal static unsafe void Copy8(this Span<byte> span, int src, int dst)
    {
        fixed (byte* ptrFrom = &span[src], ptrTo = &span[dst])
            *(UInt64*) ptrTo = *(UInt64*) ptrFrom;
    }

    #endregion

    #region Xor4 / Xor8

    internal static unsafe uint Xor4(this Span<byte> span, int offset1, int offset2)
    {
        fixed (byte* ptr1 = &span[offset1], ptr2 = &span[offset2])
            return *(uint*) ptr1 ^ *(uint*) ptr2;
    }

    internal static unsafe ulong Xor8(this Span<byte> span, int offset1, int offset2)
    {
        fixed (byte* ptr1 = &span[offset1], ptr2 = &span[offset2])
            return *(ulong*) ptr1 ^ *(ulong*) ptr2;
    }

    #endregion

    #region findMatch

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe bool findMatch(this Span<byte> src,       Span<int> hash_table, uint    h_fwd, int src_mflimit,
                                        ref  int        src_p_fwd, ref int   src_p,      ref int src_base,
                                        out  int        src_ref)
    {
        src_ref = 0;
        var findMatchAttempts = (1 << LZ4ServiceBase.SKIPSTRENGTH) + 3;
        fixed (int* ptrHash = hash_table)
            do
            {
                var h    = h_fwd;
                var step = findMatchAttempts++ >> LZ4ServiceBase.SKIPSTRENGTH;
                src_p     = src_p_fwd;
                src_p_fwd = src_p + step;

                if (src_p_fwd > src_mflimit) return false;

                h_fwd   = (src.Peek4(src_p_fwd) * LZ4ServiceBase.MULTIPLIER) >> LZ4ServiceBase.HASH_ADJUST;
                src_ref = src_base + hash_table[(int) h];

                *(ptrHash + (int) h) = (ushort) (src_p - src_base);
            } while ((src_ref < src_p - LZ4ServiceBase.MAX_DISTANCE) || !src.Equal4(src_ref, src_p));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe bool findMatch(this Span<byte> src,       Span<ushort> hash_table, uint    h_fwd, int src_mflimit,
                                        ref  int        src_p_fwd, ref int      src_p,      ref int src_base,
                                        out  int        src_ref)
    {
        src_ref = 0;
        var findMatchAttempts = (1 << LZ4ServiceBase.SKIPSTRENGTH) + 3;
        fixed (ushort* ptrHash = hash_table)
            do
            {
                var h    = h_fwd;
                var step = findMatchAttempts++ >> LZ4ServiceBase.SKIPSTRENGTH;
                src_p     = src_p_fwd;
                src_p_fwd = src_p + step;

                if (src_p_fwd > src_mflimit) return false;

                h_fwd   = (src.Peek4(src_p_fwd) * LZ4ServiceBase.MULTIPLIER) >> LZ4ServiceBase.HASH64K_ADJUST;
                src_ref = src_base + *(ptrHash + (int) h);

                *(ptrHash + (int) h) = (ushort) (src_p - src_base);
            } while (!src.Equal4(src_ref, src_p));

        return true;
    }

    #endregion

    #region lastLiterals

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe bool lastLiterals(this Span<byte> src, Span<byte> dst, int src_anchor, int src_end, int dst_end, ref int dst_p)
    {
        var lastRun = src_end - src_anchor;
        if (dst_p + lastRun + 1 + (lastRun - LZ4ServiceBase.RUN_MASK + 0xFF) / 0xFF > dst_end) return false; // compressed length >= uncompressed length

        fixed (byte* ptrDst = dst)
        {
            if (lastRun >= LZ4ServiceBase.RUN_MASK)
            {
                *(ptrDst + (dst_p++)) = LZ4ServiceBase.RUN_MASK << LZ4ServiceBase.ML_BITS;

                lastRun -= LZ4ServiceBase.RUN_MASK;

                for (; lastRun > 254; lastRun -= 0xFF)
                    *(ptrDst + (dst_p++)) = 0xFF;

                *(ptrDst + (dst_p++)) = (byte) lastRun;
            }
            else
            {
                *(ptrDst + (dst_p++)) = (byte) (lastRun << LZ4ServiceBase.ML_BITS);
            }
        }

        src.Slice(src_anchor, src_end - src_anchor).CopyTo(dst.Slice(dst_p));
        dst_p += src_end - src_anchor;
        return true;
    }

    #endregion

    #region testNextPosition

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool testNextPosition(this Span<byte> src, int     src_p,     int     src_base, ref int   src_ref,
                                        Span<byte>      dst, ref int dst_token, ref int dst_p,    Span<int> hash_table)
    {
        var h = (src.Peek4(src_p) * LZ4ServiceBase.MULTIPLIER) >> LZ4ServiceBase.HASH_ADJUST;
        src_ref             = src_base + hash_table[(int) h];
        hash_table[(int) h] = (src_p - src_base);

        if ((src_ref > src_p - (LZ4ServiceBase.MAX_DISTANCE + 1)) && src.Equal4(src_ref, src_p))
        {
            dst_token      = dst_p++;
            dst[dst_token] = 0;
            return false;
        }

        return true;
    }

    #endregion

    #region encodeLiteralLength

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool encodeLiteralLength(this Span<byte> src, int     src_p, int     src_anchor, int dst_LASTLITERALS_3,
                                           Span<byte>      dst, ref int dst_p, out int dst_token)
    {
        var length = src_p - src_anchor;
        dst_token = dst_p++;

        if (dst_p + length + (length >> 8) > dst_LASTLITERALS_3) return false; // compressed length >= uncompressed length

        if (length >= LZ4ServiceBase.RUN_MASK)
        {
            var len = length - LZ4ServiceBase.RUN_MASK;
            dst[dst_token] = (LZ4ServiceBase.RUN_MASK << LZ4ServiceBase.ML_BITS);
            if (len > 254)
            {
                do
                {
                    dst[dst_p++] =  0xFF;
                    len          -= 0xFF;
                } while (len > 254);

                dst[dst_p++] = (byte) len;
                src.Slice(src_anchor, length).CopyTo(dst.Slice(dst_p));
                dst_p += length;
                return true;
            }

            dst[dst_p++] = (byte) len;
        }
        else
        {
            dst[dst_token] = (byte) (length << LZ4ServiceBase.ML_BITS);
        }

        // Copy Literals
        if (length > 0)
        {
            var _i = dst_p + length;
            src.WildCopy(src_anchor, dst, dst_p, _i);
            dst_p = _i;
        }

        return true;
    }

    #endregion

    #region nextMatch64

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void nextMatch64(this Span<byte> src,              ref int src_p,                       ref int src_ref,            ref int src_anchor,
                                   int             src_LASTLITERALS, int     src_LASTLITERALS_STEPSIZE_1, int     src_LASTLITERALS_1, int     src_LASTLITERALS_3,
                                   Span<byte>      dst,              ref int dst_p)
    {
        // Encode Offset
        dst.Poke2(dst_p, (ushort) (src_p - src_ref));
        dst_p += 2;

        // Start Counting
        src_p      += LZ4ServiceBase.MINMATCH;
        src_ref    += LZ4ServiceBase.MINMATCH; // MinMatch already verified
        src_anchor =  src_p;

        while (src_p < src_LASTLITERALS_STEPSIZE_1)
        {
            var diff = (long) src.Xor8(src_ref, src_p);
            if (diff == 0)
            {
                src_p   += LZ4ServiceBase.STEPSIZE_64;
                src_ref += LZ4ServiceBase.STEPSIZE_64;
                continue;
            }

            src_p += LZ4Service64.DEBRUIJN_TABLE_64[((ulong) ((diff) & -(diff)) * 0x0218A392CDABBD3FL) >> 58];
            return;
        }

        if ((src_p < src_LASTLITERALS_3) && src.Equal4(src_ref, src_p))
        {
            src_p   += 4;
            src_ref += 4;
        }

        if ((src_p < src_LASTLITERALS_1) && src.Equal2(src_ref, src_p))
        {
            src_p   += 2;
            src_ref += 2;
        }

        if (src_p < src_LASTLITERALS && src[src_ref] == src[src_p]) src_p++;
    }

    #endregion

    #region encodeMatchLength

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static EncodeMatchLengthResult encodeMatchLength(this Span<byte> dst,
                                                            int             src_p, int src_mflimit, ref int src_anchor,
                                                            ref int         dst_p, int dst_token,   int     dst_LASTLITERALS_1)
    {
        var lenDiff = src_p - src_anchor;

        if (dst_p + (lenDiff >> 8) > dst_LASTLITERALS_1) return EncodeMatchLengthResult.Failed; // compressed length >= uncompressed length

        if (lenDiff >= LZ4ServiceBase.ML_MASK)
        {
            dst[dst_token] += LZ4ServiceBase.ML_MASK;
            lenDiff        -= LZ4ServiceBase.ML_MASK;
            for (; lenDiff > 509; lenDiff -= 510)
            {
                dst[dst_p++] = 0xFF;
                dst[dst_p++] = 0xFF;
            }

            if (lenDiff > 254)
            {
                lenDiff      -= 0xFF;
                dst[dst_p++] =  0xFF;
            }

            dst[dst_p++] = (byte) lenDiff;
        }
        else
        {
            dst[dst_token] += (byte) lenDiff;
        }

        // Test end of chunk
        if (src_p > src_mflimit)
        {
            src_anchor = src_p;
            return EncodeMatchLengthResult.Break;
        }

        return EncodeMatchLengthResult.Continue;
    }

    #endregion
}

internal enum EncodeMatchLengthResult
{
    Failed,
    Break,
    Continue
}