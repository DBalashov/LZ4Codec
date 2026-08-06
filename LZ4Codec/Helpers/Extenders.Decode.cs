using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LZ4.Helpers;

static class Extenders_Decode
{
    /// <summary> 0, 0, 0, -1, 0, 1, 2, 3 (stored as bytes, interpreted as sbyte) </summary>
    static ReadOnlySpan<byte> DECODER_TABLE_64 => [0, 0, 0, 0xFF, 0, 1, 2, 3];

    static ReadOnlySpan<byte> DECODER_TABLE_32 => [0, 3, 2, 3, 0, 0, 0, 0];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int Decoder64(int index) => (sbyte) Unsafe.Add(ref MemoryMarshal.GetReference(DECODER_TABLE_64), (nint) index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int Decoder32(int index) => Unsafe.Add(ref MemoryMarshal.GetReference(DECODER_TABLE_32), (nint) index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CopyLiterals(this Span<byte> src, ref int src_p, Span<byte> dst, ref int dst_p, int length, int dst_COPYLENGTH, int dst_end, out int dst_cpy)
    {
        dst_cpy = dst_p + length;

        if (dst_cpy > dst_COPYLENGTH)
        {
            if (dst_cpy != dst_end) return -src_p; // Error : not enough place for another match (min 4) + 5 literals
            src.Slice(src_p, length).CopyTo(dst.Slice(dst_p));
            src_p += length;
            return src_p;
        }

        if (dst_p < dst_cpy)
        {
            var _i = src.WildCopy(src_p, dst, dst_p, dst_cpy);
            src_p += _i;
            dst_p += _i;
        }

        src_p -= dst_p - dst_cpy;
        dst_p =  dst_cpy;

        return 0;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CopyRepeatedSequence64(this Span<byte> dst, ref int dst_p, ref int dst_ref, int length)
    {
        if ((dst_p - dst_ref) < Consts64.STEPSIZE)
        {
            var dec64 = Decoder64(dst_p - dst_ref);

            ref var p = ref MemoryMarshal.GetReference(dst);
            Unsafe.Add(ref p, (nint) dst_p + 0) = Unsafe.Add(ref p, (nint) dst_ref + 0);
            Unsafe.Add(ref p, (nint) dst_p + 1) = Unsafe.Add(ref p, (nint) dst_ref + 1);
            Unsafe.Add(ref p, (nint) dst_p + 2) = Unsafe.Add(ref p, (nint) dst_ref + 2);
            Unsafe.Add(ref p, (nint) dst_p + 3) = Unsafe.Add(ref p, (nint) dst_ref + 3);

            dst_p   += 4;
            dst_ref += 4;
            dst_ref -= Decoder32(dst_p - dst_ref);
            dst.Copy4(dst_ref, dst_p);
            dst_p   += Consts64.STEPSIZE - 4;
            dst_ref -= dec64;
        }
        else
        {
            dst.Copy8(dst_ref, dst_p);
            dst_p   += 8;
            dst_ref += 8;
        }

        return dst_p + length - (Consts64.STEPSIZE - 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CopyRepeatedSequence32(this Span<byte> dst, ref int dst_p, ref int dst_ref, int length)
    {
        if ((dst_p - dst_ref) < Consts32.STEPSIZE)
        {
            const int dec64 = 0;

            ref var p = ref MemoryMarshal.GetReference(dst);
            Unsafe.Add(ref p, (nint) dst_p + 0) = Unsafe.Add(ref p, (nint) dst_ref + 0);
            Unsafe.Add(ref p, (nint) dst_p + 1) = Unsafe.Add(ref p, (nint) dst_ref + 1);
            Unsafe.Add(ref p, (nint) dst_p + 2) = Unsafe.Add(ref p, (nint) dst_ref + 2);
            Unsafe.Add(ref p, (nint) dst_p + 3) = Unsafe.Add(ref p, (nint) dst_ref + 3);

            dst_p   += 4;
            dst_ref += 4;
            dst_ref -= Decoder32(dst_p - dst_ref);
            dst.Copy4(dst_ref, dst_p);
            dst_p   += Consts32.STEPSIZE - 4;
            dst_ref -= dec64;
        }
        else
        {
            dst.Copy4(dst_ref, dst_p);
            dst_p   += 4;
            dst_ref += 4;
        }

        return dst_p + length - (Consts32.STEPSIZE - 4);
    }
}