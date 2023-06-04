using System;
using System.Runtime.CompilerServices;

namespace LZ4.Helpers;

static class Extenders_Encode
{
    /// <summary>
    /// Decreasing this value will make the algorithm skip faster data segments considered "incompressible"
    /// This may decrease compression ratio dramatically, but will be faster on incompressible data
    /// Increasing this value will make the algorithm search more before declaring a segment "incompressible"
    /// This could improve compression a bit, but will be slower on incompressible data
    /// The default value (6) is recommended
    /// </summary>
    const int NOTCOMPRESSIBLE_DETECTIONLEVEL = 6;
    
    const int SKIPSTRENGTH = NOTCOMPRESSIBLE_DETECTIONLEVEL > 2 ? NOTCOMPRESSIBLE_DETECTIONLEVEL : 2;
    
    const int MAXD_LOG = 16;

    const int MAX_DISTANCE = (1 << MAXD_LOG) - 1;
    
    static readonly int[] DEBRUIJN_TABLE_64 =
    {
        0, 0, 0, 0, 0, 1, 1, 2, 0, 3, 1, 3, 1, 4, 2, 7,
        0, 2, 3, 6, 1, 5, 3, 5, 1, 3, 4, 4, 2, 5, 6, 7,
        7, 0, 1, 2, 3, 3, 4, 6, 2, 6, 5, 5, 3, 4, 5, 6,
        7, 1, 2, 4, 6, 4, 4, 5, 7, 2, 6, 5, 7, 6, 7, 7
    };
    
    static readonly int[] DEBRUIJN_TABLE_32 =
    {
        0, 0, 3, 0, 3, 1, 3, 0, 3, 2, 2, 1, 3, 2, 0, 1,
        3, 3, 1, 2, 2, 2, 2, 0, 3, 1, 2, 0, 1, 0, 1, 1
    };
    
    internal static int LZ4UnpackedLength(this Span<byte> span) =>
        span.Length < 8 ? 0 : BitConverter.ToInt32(span);

    internal static int LZ4PackedLength(this Span<byte> span) =>
        span.Length < 8 ? 0 : BitConverter.ToInt32(span.Slice(4));

    #region FindMatch

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe bool FindMatch(this Span<byte> src,       Span<int> hash_table, uint    h_fwd, int src_mflimit,
                                        ref  int        src_p_fwd, ref int   src_p,      ref int src_base,
                                        out  int        src_ref)
    {
        src_ref = 0;
        var findMatchAttempts = (1 << SKIPSTRENGTH) + 3;
        fixed (int* ptrHash = hash_table)
            do
            {
                var h    = h_fwd;
                var step = findMatchAttempts++ >> SKIPSTRENGTH;
                src_p     = src_p_fwd;
                src_p_fwd = src_p + step;

                if (src_p_fwd > src_mflimit) return false;

                h_fwd   = (src.Peek4(src_p_fwd) * LZ4ServiceBase.MULTIPLIER) >> LZ4ServiceBase.HASH_ADJUST;
                src_ref = src_base + hash_table[(int) h];

                *(ptrHash + (int) h) = (ushort) (src_p - src_base);
            } while ((src_ref < src_p - MAX_DISTANCE) || !src.Equal4(src_ref, src_p));

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe bool FindMatch(this Span<byte> src,       Span<ushort> hash_table, uint    h_fwd, int src_mflimit,
                                        ref  int        src_p_fwd, ref int      src_p,      ref int src_base,
                                        out  int        src_ref)
    {
        src_ref = 0;
        var findMatchAttempts = (1 << SKIPSTRENGTH) + 3;
        fixed (ushort* ptrHash = hash_table)
            do
            {
                var h    = h_fwd;
                var step = findMatchAttempts++ >> SKIPSTRENGTH;
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

    #region LastLiterals

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static unsafe bool LastLiterals(this Span<byte> src, Span<byte> dst, int src_anchor, int src_end, int dst_end, ref int dst_p)
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

    #region TestNextPosition

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool TestNextPosition(this Span<byte> src, int     src_p,     int     src_base, ref int   src_ref,
                                        Span<byte>      dst, ref int dst_token, ref int dst_p,    Span<int> hash_table)
    {
        var h = (src.Peek4(src_p) * LZ4ServiceBase.MULTIPLIER) >> LZ4ServiceBase.HASH_ADJUST;
        src_ref             = src_base + hash_table[(int) h];
        hash_table[(int) h] = (src_p - src_base);

        if ((src_ref <= src_p - (MAX_DISTANCE + 1)) || !src.Equal4(src_ref, src_p)) return true;
        
        dst_token      = dst_p++;
        dst[dst_token] = 0;
        return false;
    }

    #endregion

    #region EncodeLiteralLength

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool EncodeLiteralLength(this Span<byte> src, int src_p, int src_anchor, LastLiteralsEncode ll, Span<byte> dst, ref int dst_p, out int dst_token)
    {
        var length = src_p - src_anchor;
        dst_token = dst_p++;

        if (dst_p + length + (length >> 8) > ll.DestLiterals3) return false; // compressed length >= uncompressed length

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

    #region NextMatch32/64

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int NextMatch64(this Span<byte> src, ref int src_p, ref int src_ref, LastLiteralsEncode ll, Span<byte> dst, ref int dst_p)
    {
        // Encode Offset
        dst.Poke2(dst_p, (ushort) (src_p - src_ref));
        dst_p += 2;

        // Start Counting
        src_p      += LZ4ServiceBase.MINMATCH;
        src_ref    += LZ4ServiceBase.MINMATCH; // MinMatch already verified
        var src_anchor =  src_p;

        while (src_p < ll.SourceStepSize1)
        {
            var diff = (long) src.Xor8(src_ref, src_p);
            if (diff == 0)
            {
                src_p   += LZ4ServiceBase.STEPSIZE_64;
                src_ref += LZ4ServiceBase.STEPSIZE_64;
                continue;
            }

            src_p += DEBRUIJN_TABLE_64[((ulong) ((diff) & -(diff)) * 0x0218A392CDABBD3FL) >> 58];
            return src_anchor;
        }

        if ((src_p < ll.SourceLiterals3) && src.Equal4(src_ref, src_p))
        {
            src_p   += 4;
            src_ref += 4;
        }

        if ((src_p < ll.SourceLiterals1) && src.Equal2(src_ref, src_p))
        {
            src_p   += 2;
            src_ref += 2;
        }

        if (src_p < ll.SourceLiterals && src[src_ref] == src[src_p]) src_p++;
        return src_anchor;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int NextMatch32(this Span<byte> src, ref int src_p, ref int src_ref, LastLiteralsEncode ll, Span<byte> dst, ref int dst_p)
    {
        dst.Poke2(dst_p, (ushort) (src_p - src_ref));
        dst_p += 2;

        // Start Counting
        src_p      += LZ4ServiceBase.MINMATCH;
        src_ref    += LZ4ServiceBase.MINMATCH; // MinMatch verified
        var src_anchor =  src_p;

        while (src_p < ll.SourceStepSize1)
        {
            var diff = (int) src.Xor4(src_ref, src_p);
            if (diff == 0)
            {
                src_p   += LZ4Service32.STEPSIZE_32;
                src_ref += LZ4Service32.STEPSIZE_32;
                continue;
            }

            src_p += DEBRUIJN_TABLE_32[((uint) (diff & -(diff)) * 0x077CB531u) >> 27];
            return src_anchor;
        }

        if (src_p < ll.SourceLiterals1 && src.Equal2(src_ref, src_p))
        {
            src_p   += 2;
            src_ref += 2;
        }

        if (src_p < ll.SourceLiterals && src[src_ref] == src[src_p]) src_p++;
        return src_anchor;
    }

    #endregion

    #region EncodeMatchLength

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static EncodeMatchLengthResult EncodeMatchLength(this Span<byte> dst, int src_p, int src_mflimit, ref int src_anchor, LastLiteralsEncode ll, ref int dst_p, int dst_token)
    {
        var lenDiff = src_p - src_anchor;

        if (dst_p + (lenDiff >> 8) > ll.DestLiterals1) return EncodeMatchLengthResult.Failed; // compressed length >= uncompressed length

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