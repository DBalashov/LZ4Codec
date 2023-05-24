using System;

// ReSharper disable UselessBinaryOperation

namespace LZ4;

partial class LZ4Service64
{
    protected override int encode(Span<int> hash_table, Span<byte> _src, Span<byte> _dst)
    {
        hash_table.Fill(0);

        var elParms = new EncodeLiteralsParams()
                      {
                          dst        = _dst,
                          dst_p      = 0,
                          src        = _src,
                          src_p      = 0,
                          src_anchor = 0
                      };
        var src_base    = 0;
        var src_end     = elParms.src_p + elParms.src.Length;
        var src_mflimit = src_end       - MFLIMIT;

        //var dst_p   = 0;
        var dst_end = elParms.dst_p + elParms.dst.Length;

        var src_LASTLITERALS = src_end - LASTLITERALS;
        var lastLiterals = new LastLiterals(src_LASTLITERALS,
                                            src_LASTLITERALS - (STEPSIZE_64 - 1),
                                            src_LASTLITERALS - 1,
                                            src_LASTLITERALS - 3,
                                            dst_end          - (1 + LASTLITERALS));

        elParms.dst_LASTLITERALS_3 = dst_end - (2 + 1 + LASTLITERALS);

        // Init
        if (elParms.src.Length < MINLENGTH)
            goto _last_literals;

        // First Byte
        hash_table[(int) ((elParms.src.Peek4(elParms.src_p) * MULTIPLIER) >> HASH_ADJUST)] = elParms.src_p - src_base;
        elParms.src_p++;
        var h_fwd = (elParms.src.Peek4(elParms.src_p) * MULTIPLIER) >> HASH_ADJUST;

        // Main Loop
        while (true)
        {
            var findMatchResult = findMatch(ref elParms, ref hash_table, src_mflimit, ref h_fwd, out var src_ref, out var h);
            if (!findMatchResult) goto _last_literals;

            catchUp(ref elParms, ref src_ref);

            var r = encodeLiterals(ref elParms, out var length, out var dst_token);
            if (r == EncodeLiteralsResult.Exit) return 0;
            if (r == EncodeLiteralsResult.NextMatch) goto _next_match;

            // Copy Literals
            if (length > 0)
            {
                var _i = elParms.dst_p + length;
                elParms.src.WildCopy(elParms.src_anchor, elParms.dst, elParms.dst_p, _i);
                elParms.dst_p = _i;
            }

        _next_match:
            // Encode Offset
            elParms.dst.Poke2(elParms.dst_p, (ushort) (elParms.src_p - src_ref));
            elParms.dst_p += 2;

            // Start Counting
            startCounting(ref elParms, lastLiterals, ref src_ref);

            // Encode MatchLength
            length = elParms.src_p - elParms.src_anchor;

            if (elParms.dst_p + (length >> 8) > lastLiterals.dst_LASTLITERALS_1) return 0; // compressed length >= uncompressed length

            length = encodeMatchLength(ref elParms, length, dst_token);

            // Test end of chunk
            if (elParms.src_p > src_mflimit)
            {
                elParms.src_anchor = elParms.src_p;
                break;
            }

            // Fill table
            hash_table[(int) ((elParms.src.Peek4(elParms.src_p - 2) * MULTIPLIER) >> HASH_ADJUST)] = elParms.src_p - 2 - src_base;

            if (!testNextPosition(ref elParms, ref hash_table, ref h, out src_ref, ref dst_token))
                goto _next_match;

            // Prepare next loop
            elParms.src_anchor = elParms.src_p++;
            h_fwd              = (elParms.src.Peek4(elParms.src_p) * MULTIPLIER) >> HASH_ADJUST;
        }

    _last_literals:
        return encodeLastLiterals(ref elParms, src_end, dst_end);
    }

    EncodeLiteralsResult encodeLiterals(ref EncodeLiteralsParams p, out int length, out int dst_token)
    {
        length    = p.src_p - p.src_anchor;
        dst_token = p.dst_p++;

        if (p.dst_p + length + (length >> 8) > p.dst_LASTLITERALS_3) return EncodeLiteralsResult.Exit; // compressed length >= uncompressed length

        if (length >= RUN_MASK)
        {
            var len = length - RUN_MASK;
            p.dst[dst_token] = (RUN_MASK << ML_BITS);
            if (len > 254)
            {
                do
                {
                    p.dst[p.dst_p++] =  255;
                    len              -= 255;
                } while (len > 254);

                p.dst[p.dst_p++] = (byte) len;
                p.src.Slice(p.src_anchor, length).CopyTo(p.dst.Slice(p.dst_p));
                p.dst_p += length;
                return EncodeLiteralsResult.NextMatch;
            }

            p.dst[p.dst_p++] = (byte) len;
        }
        else
        {
            p.dst[dst_token] = (byte) (length << ML_BITS);
        }

        return EncodeLiteralsResult.NoAction;
    }

    bool findMatch(ref EncodeLiteralsParams p, ref Span<int> hash_table, int src_mflimit, ref uint h_fwd, out int src_ref, out uint h)
    {
        var findMatchAttempts = (1 << SKIPSTRENGTH) + 3;
        var src_p_fwd         = p.src_p;
        src_ref = 0;
        var src_base = 0;
        do
        {
            h = h_fwd;
            var step = findMatchAttempts++ >> SKIPSTRENGTH;
            p.src_p   = src_p_fwd;
            src_p_fwd = p.src_p + step;

            if (src_p_fwd > src_mflimit) return false; //goto _last_literals;

            h_fwd               = (p.src.Peek4(src_p_fwd) * MULTIPLIER) >> HASH_ADJUST;
            src_ref             = src_base + hash_table[(int) h];
            hash_table[(int) h] = (p.src_p - src_base);
        } while ((src_ref < p.src_p - MAX_DISTANCE) || !p.src.Equal4(src_ref, p.src_p));

        return true;
    }

    void catchUp(ref EncodeLiteralsParams p, ref int src_ref)
    {
        while ((p.src_p > p.src_anchor) && (src_ref > 0) && (p.src[p.src_p - 1] == p.src[src_ref - 1]))
        {
            p.src_p--;
            src_ref--;
        }
    }

    void startCounting(ref EncodeLiteralsParams p, LastLiterals ll, ref int src_ref)
    {
        p.src_p      += MINMATCH;
        src_ref      += MINMATCH; // MinMatch already verified
        p.src_anchor =  p.src_p;

        while (p.src_p < ll.src_LASTLITERALS_STEPSIZE_1)
        {
            var diff = (long) p.src.Xor8(src_ref, p.src_p);
            if (diff == 0)
            {
                p.src_p += STEPSIZE_64;
                src_ref += STEPSIZE_64;
                continue;
            }

            p.src_p += DEBRUIJN_TABLE_64[((ulong) ((diff) & -(diff)) * 0x0218A392CDABBD3FL) >> 58];
            return;
        }

        if ((p.src_p < ll.src_LASTLITERALS_3) && p.src.Equal4(src_ref, p.src_p))
        {
            p.src_p += 4;
            src_ref += 4;
        }

        if ((p.src_p < ll.src_LASTLITERALS_1) && p.src.Equal2(src_ref, p.src_p))
        {
            p.src_p += 2;
            src_ref += 2;
        }

        if (p.src_p < ll.src_LASTLITERALS && p.src[src_ref] == p.src[p.src_p]) p.src_p++;
    }

    int encodeMatchLength(ref EncodeLiteralsParams p, int length, int dst_token)
    {
        if (length >= ML_MASK)
        {
            p.dst[dst_token] += ML_MASK;
            length           -= ML_MASK;
            for (; length > 509; length -= 510)
            {
                p.dst[p.dst_p++] = 255;
                p.dst[p.dst_p++] = 255;
            }

            if (length > 254)
            {
                length           -= 255;
                p.dst[p.dst_p++] =  255;
            }

            p.dst[p.dst_p++] = (byte) length;
        }
        else
        {
            p.dst[dst_token] += (byte) length;
        }

        return length;
    }

    int encodeLastLiterals(ref EncodeLiteralsParams p, int src_end, int dst_end)
    {
        var lastRun = src_end - p.src_anchor;

        if (p.dst_p + lastRun + 1 + ((lastRun + 255 - RUN_MASK) / 255) > dst_end) return 0;

        if (lastRun >= RUN_MASK)
        {
            p.dst[p.dst_p++] =  RUN_MASK << ML_BITS;
            lastRun          -= RUN_MASK;
            for (; lastRun > 254; lastRun -= 255) p.dst[p.dst_p++] = 255;
            p.dst[p.dst_p++] = (byte) lastRun;
        }
        else p.dst[p.dst_p++] = (byte) (lastRun << ML_BITS);

        p.src.Slice(p.src_anchor, src_end - p.src_anchor).CopyTo(p.dst.Slice(p.dst_p));
        p.dst_p += src_end - p.src_anchor;
        return p.dst_p;
    }

    bool testNextPosition(ref EncodeLiteralsParams p, ref Span<int> hash_table, ref uint h, out int src_ref, ref int dst_token)
    {
        var src_base = 0;

        h                   = (p.src.Peek4(p.src_p) * MULTIPLIER) >> HASH_ADJUST;
        src_ref             = src_base + hash_table[(int) h];
        hash_table[(int) h] = (p.src_p - src_base);

        if ((src_ref > p.src_p - (MAX_DISTANCE + 1)) && p.src.Equal4(src_ref, p.src_p))
        {
            dst_token        = p.dst_p++;
            p.dst[dst_token] = 0;
            return false;
        }

        return true;
    }

    ref struct EncodeLiteralsParams
    {
        public int        src_p;
        public int        src_anchor;
        public int        dst_p;
        public Span<byte> src;
        public Span<byte> dst;
        public int        dst_LASTLITERALS_3;
    }

    sealed record LastLiterals(int src_LASTLITERALS, int src_LASTLITERALS_STEPSIZE_1, int src_LASTLITERALS_1, int src_LASTLITERALS_3, int dst_LASTLITERALS_1);

    enum EncodeLiteralsResult
    {
        Exit,
        NextMatch,
        NoAction
    }
}