using System;
using LZ4.Helpers;

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

        var ll = new LastLiteralsEncode(LASTLITERALS, STEPSIZE_64, src_end, dst_end);

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

            if (!src.FindMatch(hash_table, h_fwd, src_mflimit,
                               ref src_p_fwd, ref src_p, ref src_base,
                               out var src_ref)) goto _last_literals;

            // Catch up
            while (src_p > src_anchor && src_ref > 0 && src[src_p - 1] == src[src_ref - 1])
            {
                src_p--;
                src_ref--;
            }

            if (!src.EncodeLiteralLength(src_p, src_anchor, ll, dst, ref dst_p, out var dst_token)) return 0;

        _next_match:
            src_anchor = src.NextMatch64(ref src_p, ref src_ref, ll, dst, ref dst_p);

            var r = dst.EncodeMatchLength(src_p, src_mflimit, ref src_anchor, ll, ref dst_p, dst_token);
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
        return !src.LastLiterals(dst, src_anchor, src_end, dst_end, ref dst_p) ? 0 : dst_p;
    }
}