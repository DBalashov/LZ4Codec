using System;
using System.Runtime.CompilerServices;

namespace LZ4.Helpers;

static class Extenders_Decode
{
    static readonly int[] DECODER_TABLE_64 = {0, 0, 0, -1, 0, 1, 2, 3};
    static readonly int[] DECODER_TABLE_32 = {0, 3, 2, 3, 0, 0, 0, 0};

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int CopyLiterals(this Span<byte> src, ref int src_p, Span<byte> dst, ref int dst_p, int length, int dst_COPYLENGTH, int dst_end, out int dst_cpy)
    {
        dst_cpy = dst_p + length;

        if (dst_cpy > dst_COPYLENGTH)
        {
            if (dst_cpy != dst_end) return -src_p; // Error : not enough place for another match (min 4) + 5 literals
            src.Slice(src_p, length).CopyTo(dst.Slice(dst_p));
            src_p += length;
            return src_p;
        }

        if (dst_p < dst_cpy)
        {
            var _i = src.WildCopy(src_p, dst, dst_p, dst_cpy);
            src_p += _i;
            dst_p += _i;
        }

        src_p -= dst_p - dst_cpy;
        dst_p =  dst_cpy;

        return 0;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int CopyRepeatedSequence64(this Span<byte> dst, ref int dst_p, ref int dst_ref, int length)
    {
        if ((dst_p - dst_ref) < Consts64.STEPSIZE)
        {
            var dec64 = DECODER_TABLE_64[dst_p - dst_ref];

            dst[dst_p + 0] = dst[dst_ref + 0];
            dst[dst_p + 1] = dst[dst_ref + 1];
            dst[dst_p + 2] = dst[dst_ref + 2];
            dst[dst_p + 3] = dst[dst_ref + 3];

            dst_p   += 4;
            dst_ref += 4;
            dst_ref -= DECODER_TABLE_32[dst_p - dst_ref];
            dst.Copy4(dst_ref, dst_p);
            dst_p   += Consts64.STEPSIZE - 4;
            dst_ref -= dec64;
        }
        else
        {
            dst.Copy8(dst_ref, dst_p);
            dst_p   += 8;
            dst_ref += 8;
        }

        return dst_p + length - (Consts64.STEPSIZE - 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static int CopyRepeatedSequence32(this Span<byte> dst, ref int dst_p, ref int dst_ref, int length)
    {
        if ((dst_p - dst_ref) < Consts32.STEPSIZE)
        {
            const int dec64 = 0;
            dst[dst_p + 0] =  dst[dst_ref + 0];
            dst[dst_p + 1] =  dst[dst_ref + 1];
            dst[dst_p + 2] =  dst[dst_ref + 2];
            dst[dst_p + 3] =  dst[dst_ref + 3];
            dst_p          += 4;
            dst_ref        += 4;
            dst_ref        -= DECODER_TABLE_32[dst_p - dst_ref];
            dst.Copy4(dst_ref, dst_p);
            dst_p   += Consts32.STEPSIZE - 4;
            dst_ref -= dec64;
        }
        else
        {
            dst.Copy4(dst_ref, dst_p);
            dst_p   += 4;
            dst_ref += 4;
        }

        return dst_p + length - (Consts32.STEPSIZE - 4);
    }
}