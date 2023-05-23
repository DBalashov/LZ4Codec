using System;

namespace LZ4;

static class Extenders
{
    internal static int LZ4UnpackedLength(this Span<byte> span) =>
        span.Length < 8 ? 0 : BitConverter.ToInt32(span);

    internal static int LZ4PackedLength(this Span<byte> span) =>
        span.Length < 8 ? 0 : BitConverter.ToInt32(span.Slice(4));

    #region Peek2 / Peek4

    internal static ushort Peek2(this ref Span<byte> span, int offset) =>
        (ushort) (((uint) span[offset]) |
                  ((uint) span[offset + 1] << 8));

    internal static uint Peek4(this ref Span<byte> span, int offs) =>
        BitConverter.ToUInt32(span.Slice(offs));

    #endregion

    #region Equal2 / Equal4

    internal static bool Equal2(this ref Span<byte> span, int offset1, int offset2) =>
        span[offset1]     == span[offset2] &&
        span[offset1 + 1] == span[offset2 + 1];

    internal static bool Equal4(this ref Span<byte> span, int offset1, int offset2) =>
        span[offset1]     == span[offset2]     &&
        span[offset1 + 1] == span[offset2 + 1] &&
        span[offset1 + 2] == span[offset2 + 2] &&
        span[offset1 + 3] == span[offset2 + 3];

    #endregion

    internal static void Poke2(this ref Span<byte> span, int offset, ushort value)
    {
        span[offset]     = (byte) value;
        span[offset + 1] = (byte) (value >> 8);
    }

    internal static int WildCopy(this Span<byte> src, int src_0, Span<byte> dst, int dst_0, int dst_end)
    {
        var len = dst_end - dst_0;
        src.Slice(src_0, len).CopyTo(dst.Slice(dst_0));
        return len;
    }

    internal static int SecureCopy(this ref Span<byte> span, int src, int dst, int dst_end)
    {
        var diff   = dst     - src;
        var length = dst_end - dst;
        var len    = length;

        if (diff >= 16)
        {
            if (diff >= length)
            {
                span.Slice(src, length).CopyTo(span.Slice(dst));
                return length;
            }

            do
            {
                span.Slice(src, diff).CopyTo(span.Slice(dst));
                src += diff;
                dst += diff;
                len -= diff;
            } while (len >= diff);
        }

        while (len >= 4)
        {
            span[dst]     =  span[src];
            span[dst + 1] =  span[src + 1];
            span[dst + 2] =  span[src + 2];
            span[dst + 3] =  span[src + 3];
            dst           += 4;
            src           += 4;
            len           -= 4;
        }

        while (len-- > 0)
            span[dst++] = span[src++];

        return length;
    }

    #region Copy4 / Copy8

    internal static void Copy4(this ref Span<byte> span, int src, int dst) =>
        span.Slice(src, 4).CopyTo(span.Slice(dst));

    internal static void Copy8(this ref Span<byte> span, int src, int dst) =>
        span.Slice(src, 8).CopyTo(span.Slice(dst));

    #endregion

    #region Xor4 / Xor8

    internal static uint Xor4(this ref Span<byte> span, int offset1, int offset2) =>
        BitConverter.ToUInt32(span.Slice(offset1)) ^ BitConverter.ToUInt32(span.Slice(offset2));

    internal static ulong Xor8(this ref Span<byte> span, int offset1, int offset2) =>
        BitConverter.ToUInt64(span.Slice(offset1)) ^ BitConverter.ToUInt64(span.Slice(offset2));

    #endregion
}