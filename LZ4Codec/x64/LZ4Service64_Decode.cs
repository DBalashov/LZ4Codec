using System;

// ReSharper disable UselessBinaryOperation

namespace LZ4;

internal partial class LZ4Service64 : LZ4ServiceBase
{
    static readonly int[] DECODER_TABLE_64 = {0, 0, 0, -1, 0, 1, 2, 3};

    internal static readonly int[] DEBRUIJN_TABLE_64 =
    {
        0, 0, 0, 0, 0, 1, 1, 2, 0, 3, 1, 3, 1, 4, 2, 7,
        0, 2, 3, 6, 1, 5, 3, 5, 1, 3, 4, 4, 2, 5, 6, 7,
        7, 0, 1, 2, 3, 3, 4, 6, 2, 6, 5, 5, 3, 4, 5, 6,
        7, 1, 2, 4, 6, 4, 4, 5, 7, 2, 6, 5, 7, 6, 7, 7
    };

    protected override int decode(Span<byte> src, Span<byte> dst)
    {
        var src_p   = 0;
        var dst_p   = 0;
        var dst_end = dst_p + dst.Length;

        var dst_LASTLITERALS          = dst_end - LASTLITERALS;
        var dst_COPYLENGTH            = dst_end - COPYLENGTH;
        var dst_COPYLENGTH_STEPSIZE_4 = dst_end - COPYLENGTH - (STEPSIZE_64 - 4);

        // Main Loop
        while (true)
        {
            int length;

            // get runlength
            var token = src[src_p++];
            if ((length = (byte) (token >> ML_BITS)) == RUN_MASK)
            {
                int len;
                for (; (len = src[src_p++]) == 0xFF; length += 0xFF)
                {
                    /* do nothing */
                }

                length += len;
            }

            // copy literals
            var dst_cpy = dst_p + length;

            if (dst_cpy > dst_COPYLENGTH)
            {
                if (dst_cpy != dst_end) return -src_p; // Error : not enough place for another match (min 4) + 5 literals
                src.Slice(src_p, length).CopyTo(dst.Slice(dst_p));
                src_p += length;
                break; // EOF
            }

            int _i;
            if (dst_p < dst_cpy)
            {
                _i    =  src.WildCopy(src_p, dst, dst_p, dst_cpy);
                src_p += _i;
                dst_p += _i;
            }

            src_p -= dst_p - dst_cpy;
            dst_p =  dst_cpy;

            // get offset
            var dst_ref = dst_cpy - src.Peek2(src_p);
            src_p += 2;
            if (dst_ref < 0) return -src_p; // Error : offset outside destination buffer

            // get matchlength
            if ((length = (byte) (token & ML_MASK)) == ML_MASK)
            {
                for (; src[src_p] == 0xFF; length += 0xFF) src_p++;
                length += src[src_p++];
            }

            // copy repeated sequence
            if ((dst_p - dst_ref) < STEPSIZE_64)
            {
                var dec64 = DECODER_TABLE_64[dst_p - dst_ref];

                dst[dst_p + 0] =  dst[dst_ref + 0];
                dst[dst_p + 1] =  dst[dst_ref + 1];
                dst[dst_p + 2] =  dst[dst_ref + 2];
                dst[dst_p + 3] =  dst[dst_ref + 3];
                
                dst_p          += 4;
                dst_ref        += 4;
                dst_ref        -= DECODER_TABLE_32[dst_p - dst_ref];
                dst.Copy4(dst_ref, dst_p);
                dst_p   += STEPSIZE_64 - 4;
                dst_ref -= dec64;
            }
            else
            {
                dst.Copy8(dst_ref, dst_p);
                dst_p   += 8;
                dst_ref += 8;
            }

            dst_cpy = dst_p + length - (STEPSIZE_64 - 4);

            if (dst_cpy > dst_COPYLENGTH_STEPSIZE_4)
            {
                if (dst_cpy > dst_LASTLITERALS) return -src_p;; // Error : last 5 bytes must be literals
                if (dst_p < dst_COPYLENGTH)
                {
                    _i      =  dst.SecureCopy(dst_ref, dst_p, dst_COPYLENGTH);
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