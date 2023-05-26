using System;

// ReSharper disable UselessBinaryOperation

namespace LZ4;

internal partial class LZ4Service32 : LZ4ServiceBase
{
    const int STEPSIZE_32 = 4;

    static readonly int[] DEBRUIJN_TABLE_32 =
    {
        0, 0, 3, 0, 3, 1, 3, 0, 3, 2, 2, 1, 3, 2, 0, 1,
        3, 3, 1, 2, 2, 2, 2, 0, 3, 1, 2, 0, 1, 0, 1, 1
    };

    protected override int decode(Span<byte> src, Span<byte> dst)
    {
        var src_p   = 0;
        var dst_p   = 0;
        var dst_end = dst_p + dst.Length;

        var dst_LASTLITERALS          = dst_end - LASTLITERALS;
        var dst_COPYLENGTH            = dst_end - COPYLENGTH;
        var dst_COPYLENGTH_STEPSIZE_4 = dst_end - COPYLENGTH - (STEPSIZE_32 - 4);

        // Main Loop
        while (true)
        {
            int length;

            // get runlength
            var token = src[src_p++];
            if ((length = (token >> ML_BITS)) == RUN_MASK)
            {
                int len;
                for (; (len = src[src_p++]) == 255; length += 255)
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
            if ((length = (token & ML_MASK)) == ML_MASK)
            {
                for (; src[src_p] == 255; length += 255) src_p++;
                length += src[src_p++];
            }

            // copy repeated sequence
            if ((dst_p - dst_ref) < STEPSIZE_32)
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
                dst_p   += STEPSIZE_32 - 4;
                dst_ref -= dec64;
            }
            else
            {
                dst.Copy4(dst_ref, dst_p);
                dst_p   += 4;
                dst_ref += 4;
            }

            dst_cpy = dst_p + length - (STEPSIZE_32 - 4);

            if (dst_cpy > dst_COPYLENGTH_STEPSIZE_4)
            {
                if (dst_cpy > dst_LASTLITERALS) return -src_p; // Error : last 5 bytes must be literals
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

        // end of decoding
        return src_p;
    }
}