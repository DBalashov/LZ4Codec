using System;
using System.Runtime.InteropServices;

namespace LZ4.Helpers;

static class LowLevel
{
    #region Peek2 / Peek4

    internal static unsafe ushort Peek2(this Span<byte> span, int offset)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            return *(ushort*) (ptr + offset);
    }

    internal static unsafe uint Peek4(this Span<byte> span, int offs)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            return *(uint*) (ptr + offs);
    }

    #endregion

    #region Equal2 / Equal4

    internal static unsafe bool Equal2(this Span<byte> span, int offset1, int offset2)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            return *(ushort*) (ptr + offset1) == *(ushort*) (ptr + offset2);
    }

    internal static unsafe bool Equal4(this Span<byte> span, int offset1, int offset2)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            return *(uint*) (ptr + offset1) == *(uint*) (ptr + offset2);
    }

    #endregion

    internal static unsafe void Poke2(this Span<byte> span, int offset, ushort value)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(span))
            *(ushort*) (ptr + offset) = value;
    }

    internal static unsafe int WildCopy(this Span<byte> src, int srcOffset, Span<byte> dst, int dstOffset, int dstOffsetEnd)
    {
        var len = dstOffsetEnd - dstOffset;

        fixed (byte* ptrFrom = &MemoryMarshal.GetReference(src))
        fixed (byte* ptrTo = &MemoryMarshal.GetReference(dst))
            Buffer.MemoryCopy(ptrFrom + srcOffset, ptrTo + dstOffset, len, len);

        return len;
    }

    internal static unsafe int SecureCopy(this Span<byte> span, int src, int dst, int dst_end)
    {
        var diff   = dst     - src;
        var length = dst_end - dst;
        var len    = length;

        if (diff >= 16)
        {
            if (diff >= length)
            {
                fixed (byte* ptr = &MemoryMarshal.GetReference(span))
                    Buffer.MemoryCopy(ptr + src, ptr + dst, length, length);
                return length;
            }

            do
            {
                fixed (byte* ptr = &MemoryMarshal.GetReference(span))
                    Buffer.MemoryCopy(ptr + src, ptr + dst, diff, diff);

                src += diff;
                dst += diff;
                len -= diff;
            } while (len >= diff);
        }

        while (len >= 4)
        {
            fixed (byte* ptrFrom = &span[src], ptrTo = &span[dst])
                *(uint*) ptrTo = *(uint*) ptrFrom;

            dst += 4;
            src += 4;
            len -= 4;
        }

        while (len-- > 0)
            span[dst++] = span[src++];

        return length;
    }

    #region Copy4 / Copy8

    internal static unsafe void Copy4(this Span<byte> span, int src, int dst)
    {
        fixed (byte* ptrFrom = &span[src], ptrTo = &span[dst])
            *(uint*) ptrTo = *(uint*) ptrFrom;
    }

    internal static unsafe void Copy8(this Span<byte> span, int src, int dst)
    {
        fixed (byte* ptrFrom = &span[src], ptrTo = &span[dst])
            *(UInt64*) ptrTo = *(UInt64*) ptrFrom;
    }

    #endregion

    #region Xor4 / Xor8

    internal static unsafe uint Xor4(this Span<byte> span, int offset1, int offset2)
    {
        fixed (byte* ptr1 = &span[offset1], ptr2 = &span[offset2])
            return *(uint*) ptr1 ^ *(uint*) ptr2;
    }

    internal static unsafe ulong Xor8(this Span<byte> span, int offset1, int offset2)
    {
        fixed (byte* ptr1 = &span[offset1], ptr2 = &span[offset2])
            return *(ulong*) ptr1 ^ *(ulong*) ptr2;
    }

    #endregion
}