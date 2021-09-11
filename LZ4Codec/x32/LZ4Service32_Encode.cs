using System;

// ReSharper disable UselessBinaryOperation

namespace LZ4
{
    partial class LZ4Service32
    {
        protected override int encode(Span<int> hash_table, Span<byte> src, Span<byte> dst)
        {
            hash_table.Fill(0);

            var src_p       = 0;
            var src_base    = 0;
            var src_anchor  = src_p;
            var src_end     = src_p + src.Length;
            var src_mflimit = src_end - MFLIMIT;

            var dst_p   = 0;
            var dst_end = dst_p + dst.Length;

            var src_LASTLITERALS   = src_end - LASTLITERALS;
            var src_LASTLITERALS_1 = src_LASTLITERALS - 1;

            var src_LASTLITERALS_STEPSIZE_1 = src_LASTLITERALS - (STEPSIZE_32 - 1);
            var dst_LASTLITERALS_1          = dst_end - (1 + LASTLITERALS);
            var dst_LASTLITERALS_3          = dst_end - (2 + 1 + LASTLITERALS);

            // Init
            if (src.Length < MINLENGTH) goto _last_literals;

            // First Byte
            hash_table[(int)((src.Peek4(src_p) * MULTIPLIER) >> HASH_ADJUST)] = src_p - src_base;
            src_p++;
            var h_fwd = (src.Peek4(src_p) * MULTIPLIER) >> HASH_ADJUST;

            // Main Loop
            while (true)
            {
                var findMatchAttempts = (1 << SKIPSTRENGTH) + 3;
                var src_p_fwd         = src_p;
                int src_ref;

                // Find a match
                uint h;
                do
                {
                    h = h_fwd;
                    var step = findMatchAttempts++ >> SKIPSTRENGTH;
                    src_p     = src_p_fwd;
                    src_p_fwd = src_p + step;

                    if (src_p_fwd > src_mflimit) goto _last_literals;

                    h_fwd              = (src.Peek4(src_p_fwd) * MULTIPLIER) >> HASH_ADJUST;
                    src_ref            = src_base + hash_table[(int)h];
                    hash_table[(int)h] = src_p - src_base;
                } while ((src_ref < src_p - MAX_DISTANCE) || !src.Equal4(src_ref, src_p));

                // Catch up
                while (src_p > src_anchor && src_ref > 0 && src[src_p - 1] == src[src_ref - 1])
                {
                    src_p--;
                    src_ref--;
                }

                // Encode Literal length
                var length    = (src_p - src_anchor);
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

                        dst[dst_p++] = (byte)len;
                        src.Slice(src_anchor, length).CopyTo(dst.Slice(dst_p));
                        dst_p += length;
                        goto _next_match;
                    }
                    else
                        dst[dst_p++] = (byte)len;
                }
                else
                {
                    dst[dst_token] = (byte)(length << ML_BITS);
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
                dst.Poke2(dst_p, (ushort)(src_p - src_ref));
                dst_p += 2;

                // Start Counting
                src_p      += MINMATCH;
                src_ref    += MINMATCH; // MinMatch already verified
                src_anchor =  src_p;

                while (src_p < src_LASTLITERALS_STEPSIZE_1)
                {
                    var diff = (int)src.Xor4(src_ref, src_p);
                    if (diff == 0)
                    {
                        src_p   += STEPSIZE_32;
                        src_ref += STEPSIZE_32;
                        continue;
                    }

                    src_p += DEBRUIJN_TABLE_32[((uint)(diff & -(diff)) * 0x077CB531u) >> 27];
                    goto _endCount;
                }

                if (src_p < src_LASTLITERALS_1 && src.Equal2(src_ref, src_p))
                {
                    src_p   += 2;
                    src_ref += 2;
                }

                if (src_p < src_LASTLITERALS && src[src_ref] == src[src_p]) src_p++;

                _endCount:
                // Encode MatchLength
                length = (src_p - src_anchor);

                if (dst_p + (length >> 8) > dst_LASTLITERALS_1) return 0; // compressed length >= uncompressed length

                if (length >= ML_MASK)
                {
                    dst[dst_token] += ML_MASK;
                    length         -= ML_MASK;
                    for (; length > 509; length -= 510)
                    {
                        dst[dst_p++] = 255;
                        dst[dst_p++] = 255;
                    }

                    if (length > 254)
                    {
                        length       -= 255;
                        dst[dst_p++] =  255;
                    }

                    dst[dst_p++] = (byte)length;
                }
                else
                {
                    dst[dst_token] += (byte)length;
                }

                // Test end of chunk
                if (src_p > src_mflimit)
                {
                    src_anchor = src_p;
                    break;
                }

                // Fill table
                hash_table[(int)((src.Peek4(src_p - 2) * MULTIPLIER) >> HASH_ADJUST)] = src_p - 2 - src_base;

                // Test next position

                h                  = (src.Peek4(src_p) * MULTIPLIER) >> HASH_ADJUST;
                src_ref            = src_base + hash_table[(int)h];
                hash_table[(int)h] = src_p - src_base;

                if ((src_ref > src_p - (MAX_DISTANCE + 1)) && src.Equal4(src_ref, src_p))
                {
                    dst_token      = dst_p++;
                    dst[dst_token] = 0;
                    goto _next_match;
                }

                // Prepare next loop
                src_anchor = src_p++;
                h_fwd      = (src.Peek4(src_p) * MULTIPLIER) >> HASH_ADJUST;
            }

            _last_literals:
            // Encode Last Literals
            {
                var lastRun = src_end - src_anchor;
                if (dst_p + lastRun + 1 + ((lastRun + 255 - RUN_MASK) / 255) > dst_end) return 0;

                if (lastRun >= RUN_MASK)
                {
                    dst[dst_p++] =  RUN_MASK << ML_BITS;
                    lastRun      -= RUN_MASK;
                    for (; lastRun > 254; lastRun -= 255) dst[dst_p++] = 255;
                    dst[dst_p++] = (byte)lastRun;
                }
                else dst[dst_p++] = (byte)(lastRun << ML_BITS);

                src.Slice(src_anchor, src_end - src_anchor).CopyTo(dst.Slice(dst_p));
                dst_p += src_end - src_anchor;
            }

            // End
            return dst_p;
        }
    }
}