using System;
using System.Runtime.CompilerServices;

// ReSharper disable UselessBinaryOperation

namespace LZ4;

partial class LZ4Service64
{
    /// <summary> for small data (&lt;=64K) </summary>
    protected override int encodeSmall(Span<ushort> hash_table, Span<byte> src, Span<byte> dst)
    {
        hash_table.Fill(0);

        var src_p       = 0;
        var src_anchor  = src_p;
        var src_base    = src_p;
        var src_end     = src.Length;
        var src_mflimit = src.Length - MFLIMIT;

        var dst_p   = 0;
        var dst_end = dst_p + dst.Length;

        var src_LASTLITERALS   = src_end          - LASTLITERALS;
        var src_LASTLITERALS_1 = src_LASTLITERALS - 1;

        var src_LASTLITERALS_3 = src_LASTLITERALS - 3;

        var src_LASTLITERALS_STEPSIZE_1 = src_LASTLITERALS - (STEPSIZE_64 - 1);
        var dst_LASTLITERALS_1          = dst_end          - (1           + LASTLITERALS);
        var dst_LASTLITERALS_3          = dst_end          - (2           + 1 + LASTLITERALS);

        // Init
        if (src.Length < MINLENGTH)
            goto _last_literals;

        // First Byte
        src_p++;
        var h_fwd = (src.Peek4(src_p) * MULTIPLIER) >> HASH64K_ADJUST;

        // Main Loop
        while (true)
        {
            var src_p_fwd = src_p;

            if (!src.findMatch(hash_table, h_fwd, src_mflimit,
                               ref src_p_fwd, ref src_p, ref src_base,
                               out var src_ref)) goto _last_literals;

            // Catch up
            while (src_p > src_anchor && src_ref > 0 && src[src_p - 1] == src[src_ref - 1])
            {
                src_p--;
                src_ref--;
            }

            // Encode Literal length
            var length    = src_p - src_anchor;
            var dst_token = dst_p++;

            if (dst_p + length + (length >> 8) > dst_LASTLITERALS_3) return 0; // compressed length >= uncompressed length

            if (length >= RUN_MASK)
            {
                var len = length - RUN_MASK;
                dst[dst_token] = RUN_MASK << ML_BITS;
                if (len > 254)
                {
                    do
                    {
                        dst[dst_p++] =  255;
                        len          -= 255;
                    } while (len > 254);

                    dst[dst_p++] = (byte) len;
                    src.Slice(src_anchor, length).CopyTo(dst.Slice(dst_p));
                    dst_p += length;
                    goto _next_match;
                }

                dst[dst_p++] = (byte) len;
            }
            else
            {
                dst[dst_token] = (byte) (length << ML_BITS);
            }

            // Copy Literals
            if (length > 0)
            {
                var _i = dst_p + length;
                src.WildCopy(src_anchor, dst, dst_p, _i);
                dst_p = _i;
            }

        _next_match:
            // Encode Offset
            dst.Poke2(dst_p, (ushort) (src_p - src_ref));
            dst_p += 2;

            // Start Counting
            src_p      += MINMATCH;
            src_ref    += MINMATCH; // MinMatch verified
            src_anchor =  src_p;

            while (src_p < src_LASTLITERALS_STEPSIZE_1)
            {
                var diff = (long) src.Xor8(src_ref, src_p);
                if (diff == 0)
                {
                    src_p   += STEPSIZE_64;
                    src_ref += STEPSIZE_64;
                    continue;
                }

                src_p += DEBRUIJN_TABLE_64[((ulong) (diff & (-diff)) * 0x0218A392CDABBD3FL) >> 58];
                goto _endCount;
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

        _endCount:
            // Encode MatchLength
            var lenDiff = src_p - src_anchor;
            if (dst_p + (lenDiff >> 8) > dst_LASTLITERALS_1) return 0; // compressed length >= uncompressed length

            if (lenDiff >= ML_MASK)
            {
                dst[dst_token] += ML_MASK;
                lenDiff        -= ML_MASK;
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
                break;
            }

            // Fill table
            hash_table[(int) ((src.Peek4(src_p - 2) * MULTIPLIER) >> HASH64K_ADJUST)] = (ushort) (src_p - 2 - src_base);

            // Test next position
            var ha = (src.Peek4(src_p) * MULTIPLIER) >> HASH64K_ADJUST;
            src_ref              = src_base + hash_table[(int) ha];
            hash_table[(int) ha] = (ushort) (src_p - src_base);

            if (src.Equal4(src_ref, src_p))
            {
                dst_token      = dst_p++;
                dst[dst_token] = 0;
                goto _next_match;
            }

            // Prepare next loop
            src_anchor = src_p++;
            h_fwd      = (src.Peek4(src_p) * MULTIPLIER) >> HASH64K_ADJUST;
        }

    _last_literals:
        return !src.lastLiterals(dst, src_anchor, src_end, dst_end, ref dst_p) ? 0 : dst_p;
    }
}