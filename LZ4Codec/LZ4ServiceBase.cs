using System;
using System.Buffers;
using LZ4.Helpers;

namespace LZ4;

internal abstract class LZ4ServiceBase
{
    protected abstract int Encode(Span<int>         hash_table, Span<byte> src, Span<byte> dst);
    protected abstract int EncodeSmall(Span<ushort> hash_table, Span<byte> src, Span<byte> dst);
    protected abstract int Decode(Span<byte>        src,        Span<byte> dst);

    public byte[] Decode(Span<byte> inputBuffer)
    {
        // 1st pass: validate headers and calculate total unpacked size, to allocate resulting array exactly once
        var totalLength = 0;
        var scan        = inputBuffer;
        while (scan.Length >= 8)
        {
            var unpackedLength = scan.LZ4UnpackedLength();
            var packedLength   = scan.LZ4PackedLength();
            if (unpackedLength == 0 && packedLength == 0) break;

            ValidateBlock(packedLength, unpackedLength);

            totalLength += unpackedLength;
            scan        =  scan.Slice(8 + (packedLength == 0 ? unpackedLength : packedLength));
        }

        if (totalLength == 0) return [];

        // 2nd pass: unpack directly into resulting array, without intermediate buffers
        var result   = new byte[totalLength];
        var resultSp = result.AsSpan();
        var offset   = 0;

        while (inputBuffer.Length >= 8)
        {
            var unpackedLength = inputBuffer.LZ4UnpackedLength();
            var packedLength   = inputBuffer.LZ4PackedLength();
            if (unpackedLength == 0 && packedLength == 0) break;

            if (packedLength == 0)
            {
                inputBuffer.Slice(8, unpackedLength).CopyTo(resultSp.Slice(offset));
                inputBuffer = inputBuffer.Slice(8 + unpackedLength);
            }
            else
            {
                var r = Decode(inputBuffer.Slice(8, packedLength),
                               resultSp.Slice(offset, unpackedLength));
                if (r < 0) throw new InvalidOperationException($"Can't unpack at position {-r}");

                inputBuffer = inputBuffer.Slice(8 + packedLength);
            }

            offset += unpackedLength;
        }

        return result;
    }

    static void ValidateBlock(int packedLength, int unpackedLength)
    {
        if (packedLength < 0 || unpackedLength < 0)
            throw new InvalidOperationException($"PackedLength or UnpackedLength has invalid value ({packedLength} / {unpackedLength})");
        if (packedLength >= unpackedLength)
            throw new InvalidOperationException($"PackedLength > UnpackedLength ({packedLength} > {unpackedLength})");
    }

    [ThreadStatic] static ushort[] hashTableSmall;
    [ThreadStatic] static int[]    hashTableBig;

    public Span<byte> Encode(Span<byte> input)
    {
        if (input.Length == 0)
            return Span<byte>.Empty;

        // temporary buffer of worst-case size: final array is allocated with exact length only
        var work = ArrayPool<byte>.Shared.Rent(input.Length + 8);
        try
        {
            var outputBuffer = work.AsSpan(0, input.Length + 8);
            int packedLength;

            if (input.Length < Consts.LZ4_64KLIMIT)
            {
                var hashTable = hashTableSmall ??= new ushort[Consts64.HASH_TABLESIZE];
                packedLength = EncodeSmall(hashTable, input, outputBuffer.Slice(8));
            }
            else
            {
                var hashTable = hashTableBig ??= new int[Consts32.HASH_TABLESIZE];
                packedLength = Encode(hashTable, input, outputBuffer.Slice(8));
            }

            BitConverter.TryWriteBytes(outputBuffer,          input.Length);
            BitConverter.TryWriteBytes(outputBuffer.Slice(4), packedLength);

            if (packedLength > 0)
                return outputBuffer.Slice(0, packedLength + 8).ToArray();

            // not compressible: store as is
            var stored = new byte[input.Length + 8];
            outputBuffer.Slice(0, 8).CopyTo(stored);
            input.CopyTo(stored.AsSpan(8));
            return stored;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(work);
        }
    }
}