using System;
using System.Buffers;
using System.IO;
using LZ4.Helpers;

namespace LZ4;

internal abstract class LZ4ServiceBase
{
    protected abstract int Encode(Span<int>         hash_table, Span<byte> src, Span<byte> dst);
    protected abstract int EncodeSmall(Span<ushort> hash_table, Span<byte> src, Span<byte> dst);
    protected abstract int Decode(Span<byte>        src,        Span<byte> dst);

    public byte[] Decode(Span<byte> inputBuffer)
    {
        using var stm = new MemoryStream();
        while (true)
        {
            if (inputBuffer.Length < 8) break;

            var unpackedLength = inputBuffer.LZ4UnpackedLength();
            var packedLength   = inputBuffer.LZ4PackedLength();
            if (unpackedLength == 0 && packedLength == 0) break;

            if (packedLength < 0 || unpackedLength < 0)
                throw new InvalidOperationException($"PackedLength or UnpackedLength has invalid value ({packedLength} / {unpackedLength})");
            if (packedLength >= unpackedLength)
                throw new InvalidOperationException($"PackedLength > UnpackedLength ({packedLength} > {unpackedLength})");

            if (packedLength == 0)
            {
                var slice = inputBuffer.Slice(8, unpackedLength);
                stm.Write(slice);
                inputBuffer = inputBuffer.Slice(8 + unpackedLength);
            }
            else
            {
                var outputBuffer = ArrayPool<byte>.Shared.Rent(unpackedLength);
                try
                {
                    var r = Decode(inputBuffer.Slice(8, packedLength),
                                   outputBuffer.AsSpan(0, unpackedLength));
                    if (r < 0) throw new InvalidOperationException($"Can't unpack at position {-r}");

                    stm.Write(outputBuffer.AsSpan(0, unpackedLength));
                    inputBuffer = inputBuffer.Slice(8 + packedLength);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(outputBuffer);
                }
            }
        }

        return stm.ToArray();
    }

    public Span<byte> Encode(Span<byte> input)
    {
        if (input.Length == 0)
            return Array.Empty<byte>().AsSpan();

        var outputBuffer = new byte[input.Length + 8].AsSpan();
        int packedLength;

        if (input.Length < Consts.LZ4_64KLIMIT)
        {
            var hashTable = ArrayPool<ushort>.Shared.Rent(Consts64.HASH_TABLESIZE);
            try
            {
                packedLength = EncodeSmall(hashTable.AsSpan(0, Consts64.HASH_TABLESIZE), input, outputBuffer.Slice(8));
            }
            finally
            {
                ArrayPool<ushort>.Shared.Return(hashTable);
            }
        }
        else
        {
            var hashTable = ArrayPool<int>.Shared.Rent(Consts32.HASH_TABLESIZE);
            try
            {
                packedLength = Encode(hashTable.AsSpan(0, Consts32.HASH_TABLESIZE), input, outputBuffer.Slice(8));
            }
            finally
            {
                ArrayPool<int>.Shared.Return(hashTable);
            }
        }

        BitConverter.TryWriteBytes(outputBuffer,          input.Length);
        BitConverter.TryWriteBytes(outputBuffer.Slice(4), packedLength);

        if (packedLength > 0)
            return outputBuffer.Slice(0, packedLength + 8);

        input.CopyTo(outputBuffer.Slice(8));
        return outputBuffer.Slice(0, input.Length + 8);
    }
}