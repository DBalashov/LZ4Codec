using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LZ4.Helpers;

static class LowLevel
{
    #region Peek2 / Peek4

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ushort Peek2(this Span<byte> span, int offset) =>
        Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint) offset));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Peek4(this Span<byte> span, int offs) =>
        Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint) offs));

    #endregion

    #region Equal2 / Equal4

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Equal2(this Span<byte> span, int offset1, int offset2)
    {
        ref var p = ref MemoryMarshal.GetReference(span);
        return Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref p, (nint) offset1)) ==
               Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref p, (nint) offset2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Equal4(this Span<byte> span, int offset1, int offset2)
    {
        ref var p = ref MemoryMarshal.GetReference(span);
        return Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref p, (nint) offset1)) ==
               Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref p, (nint) offset2));
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Poke2(this Span<byte> span, int offset, ushort value) =>
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(span), (nint) offset), value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int WildCopy(this Span<byte> src, int srcOffset, Span<byte> dst, int dstOffset, int dstOffsetEnd)
    {
        var len = dstOffsetEnd - dstOffset;

        Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref MemoryMarshal.GetReference(dst), (nint) dstOffset),
                                  ref Unsafe.Add(ref MemoryMarshal.GetReference(src), (nint) srcOffset),
                                  (uint) len);

        return len;
    }

    internal static int SecureCopy(this Span<byte> span, int src, int dst, int dst_end)
    {
        var diff   = dst     - src;
        var length = dst_end - dst;
        var len    = length;

        ref var origin = ref MemoryMarshal.GetReference(span);

        if (diff >= 16)
        {
            if (diff >= length)
            {
                Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref origin, (nint) dst),
                                          ref Unsafe.Add(ref origin, (nint) src),
                                          (uint) length);
                return length;
            }

            do
            {
                Unsafe.CopyBlockUnaligned(ref Unsafe.Add(ref origin, (nint) dst),
                                          ref Unsafe.Add(ref origin, (nint) src),
                                          (uint) diff);

                src += diff;
                dst += diff;
                len -= diff;
            } while (len >= diff);
        }

        while (len >= 4)
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref origin, (nint) dst),
                                  Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref origin, (nint) src)));

            dst += 4;
            src += 4;
            len -= 4;
        }

        while (len-- > 0)
            Unsafe.Add(ref origin, (nint) dst++) = Unsafe.Add(ref origin, (nint) src++);

        return length;
    }

    #region Copy4 / Copy8

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Copy4(this Span<byte> span, int src, int dst)
    {
        ref var p = ref MemoryMarshal.GetReference(span);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref p, (nint) dst),
                              Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref p, (nint) src)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Copy8(this Span<byte> span, int src, int dst)
    {
        ref var p = ref MemoryMarshal.GetReference(span);
        Unsafe.WriteUnaligned(ref Unsafe.Add(ref p, (nint) dst),
                              Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref p, (nint) src)));
    }

    #endregion

    #region Xor4 / Xor8

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static uint Xor4(this Span<byte> span, int offset1, int offset2)
    {
        ref var p = ref MemoryMarshal.GetReference(span);
        return Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref p, (nint) offset1)) ^
               Unsafe.ReadUnaligned<uint>(ref Unsafe.Add(ref p, (nint) offset2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Xor8(this Span<byte> span, int offset1, int offset2)
    {
        ref var p = ref MemoryMarshal.GetReference(span);
        return Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref p, (nint) offset1)) ^
               Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref p, (nint) offset2));
    }

    #endregion
}
