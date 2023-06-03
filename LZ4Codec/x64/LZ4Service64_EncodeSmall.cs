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

            if (!src.encodeLiteralLength(src_p, src_anchor, dst_LASTLITERALS_3, dst, ref dst_p, out var dst_token)) return 0;

        _next_match:
            src.nextMatch64(ref src_p, ref src_ref, ref src_anchor, src_LASTLITERALS, src_LASTLITERALS_STEPSIZE_1, src_LASTLITERALS_1, src_LASTLITERALS_3,
                            dst, ref dst_p);
        
            var r = dst.encodeMatchLength(src_p, src_mflimit, ref src_anchor, ref dst_p, dst_token, dst_LASTLITERALS_1);
            if (r == EncodeMatchLengthResult.Failed) return 0;
            if (r == EncodeMatchLengthResult.Break) break;

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