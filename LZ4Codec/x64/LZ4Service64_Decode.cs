using System;
using LZ4.Helpers;

// ReSharper disable UselessBinaryOperation

namespace LZ4;

internal partial class LZ4Service64 : LZ4ServiceBase
{
    protected override int Decode(Span<byte> src, Span<byte> dst)
    {
        var src_p   = 0;
        var dst_p   = 0;
        var dst_end = dst_p + dst.Length;

        var dst_LASTLITERALS          = dst_end - Consts.LASTLITERALS;
        var dst_COPYLENGTH            = dst_end - Consts.COPYLENGTH;
        var dst_COPYLENGTH_STEPSIZE_4 = dst_end - Consts.COPYLENGTH - (Consts64.STEPSIZE - 4);

        // Main Loop
        while (true)
        {
            int length;

            // get runlength
            var token = src[src_p++];
            if ((length = (byte) (token >> Consts.ML_BITS)) == Consts.RUN_MASK)
            {
                int len;
                for (; (len = src[src_p++]) == 0xFF; length += 0xFF)
                {
                    /* do nothing */
                }

                length += len;
            }

            var r = src.CopyLiterals(ref src_p, dst, ref dst_p, length, dst_COPYLENGTH, dst_end, out var dst_cpy);
            if (r < 0) return r;
            if (r > 0) break;

            // get offset
            var dst_ref = dst_cpy - src.Peek2(src_p);
            src_p += 2;
            if (dst_ref < 0) return -src_p; // Error : offset outside destination buffer

            // get matchlength
            if ((length = (byte) (token & Consts.ML_MASK)) == Consts.ML_MASK)
            {
                for (; src[src_p] == 0xFF; length += 0xFF) src_p++;
                length += src[src_p++];
            }
            
            dst_cpy = dst.CopyRepeatedSequence64(ref dst_p, ref dst_ref, length);

            if (dst_cpy > dst_COPYLENGTH_STEPSIZE_4)
            {
                if (dst_cpy > dst_LASTLITERALS) return -src_p; // Error : last 5 bytes must be literals
                if (dst_p < dst_COPYLENGTH)
                {
                    var _i = dst.SecureCopy(dst_ref, dst_p, dst_COPYLENGTH);
                    dst_ref += _i;
                    dst_p   += _i;
                }

                while (dst_p < dst_cpy) dst[dst_p++] = dst[dst_ref++];
                dst_p = dst_cpy;
                continue;
            }

            if (dst_p < dst_cpy)
                dst.SecureCopy(dst_ref, dst_p, dst_cpy);

            dst_p = dst_cpy; // correction
        }

        return src_p;
    }
}