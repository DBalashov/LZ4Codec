using System;
using System.Buffers;

namespace LZ4;

internal abstract class LZ4ServiceBase
{
    protected static readonly ArrayPool<ushort> PoolUShorts = ArrayPool<ushort>.Shared;
    protected static readonly ArrayPool<int>    PoolInts    = ArrayPool<int>.Shared;

    /// <summary>
    /// Memory usage formula : N->2^N Bytes (examples : 10 -> 1KB; 12 -> 4KB ; 16 -> 64KB; 20 -> 1MB; etc.)
    /// Increasing memory usage improves compression ratio
    /// Reduced memory usage can improve speed, due to cache effect
    /// Default value is 14, for 16KB, which nicely fits into Intel x86 L1 cache
    /// </summary>
    protected const int MEMORY_USAGE = 14;

    protected const int COPYLENGTH   = 8;
    protected const int MINMATCH     = 4;
    protected const int MFLIMIT      = COPYLENGTH + MINMATCH;
    protected const int LZ4_64KLIMIT = (1 << 16)  + (MFLIMIT - 1);

    protected const int HASH_LOG       = MEMORY_USAGE - 2;
    protected const int HASH_TABLESIZE = 1 << HASH_LOG;
    protected const int HASH_ADJUST    = MINMATCH * 8 - HASH_LOG;

    protected const int HASH64K_LOG       = HASH_LOG + 1;
    protected const int HASH64K_TABLESIZE = 1 << HASH64K_LOG;
    protected const int HASH64K_ADJUST    = MINMATCH * 8 - HASH64K_LOG;

    protected const int LASTLITERALS = 5;

    protected const int MINLENGTH = MFLIMIT + 1;

    protected const int MAXD_LOG = 16;

    protected const int MAX_DISTANCE = (1 << MAXD_LOG) - 1;
    protected const int ML_BITS      = 4;
    protected const int ML_MASK      = (1 << ML_BITS)  - 1;
    protected const int RUN_BITS     = 8               - ML_BITS;
    protected const int RUN_MASK     = (1 << RUN_BITS) - 1;
    protected const int STEPSIZE_64  = 8;

    protected const uint MULTIPLIER = 2654435761u;

    /// <summary>
    /// Decreasing this value will make the algorithm skip faster data segments considered "incompressible"
    /// This may decrease compression ratio dramatically, but will be faster on incompressible data
    /// Increasing this value will make the algorithm search more before declaring a segment "incompressible"
    /// This could improve compression a bit, but will be slower on incompressible data
    /// The default value (6) is recommended
    /// </summary>
    protected const int NOTCOMPRESSIBLE_DETECTIONLEVEL = 6;

    protected const int SKIPSTRENGTH = NOTCOMPRESSIBLE_DETECTIONLEVEL > 2 ? NOTCOMPRESSIBLE_DETECTIONLEVEL : 2;

    protected static readonly int[] DECODER_TABLE_32 = {0, 3, 2, 3, 0, 0, 0, 0};

    protected abstract int encode(Span<int>         hash_table, Span<byte> src, Span<byte> dst);
    protected abstract int encodeSmall(Span<ushort> hash_table, Span<byte> src, Span<byte> dst);
    protected abstract int decode(Span<byte>        src,        Span<byte> dst);

    #region public Decode / Encode

    public virtual Span<byte> Decode(Span<byte> inputBuffer)
    {
        if (inputBuffer.Length < 8)
            return Array.Empty<byte>().AsSpan();

        var unpackedLength = inputBuffer.LZ4UnpackedLength();
        var packedLength   = inputBuffer.LZ4PackedLength();

        if (packedLength == 0)
            return inputBuffer.Slice(8);

        if (packedLength < 0 || unpackedLength < 0)
            throw new InvalidOperationException($"PackedLength or UnpackedLength has invalid value ({packedLength} / {unpackedLength})");

        if (packedLength >= unpackedLength)
            throw new InvalidOperationException($"PackedLength > UnpackedLength ({packedLength} > {unpackedLength})");

        var outputBuffer = new byte[unpackedLength];
        var r            = decode(inputBuffer.Slice(8, packedLength), outputBuffer);
        return r < 0
                   ? throw new InvalidOperationException($"Can't unpack at position {-r}")
                   : outputBuffer;
    }

    public Span<byte> Encode(Span<byte> input)
    {
        if (input.Length == 0)
            return Array.Empty<byte>().AsSpan();

        var outputBuffer = new byte[input.Length + 8].AsSpan();
        int packedLength;

        if (input.Length < LZ4_64KLIMIT)
        {
            var hashTable = PoolUShorts.Rent(HASH64K_TABLESIZE);
            try
            {
                packedLength = encodeSmall(hashTable.AsSpan(0, HASH64K_TABLESIZE), input, outputBuffer.Slice(8));
            }
            finally
            {
                PoolUShorts.Return(hashTable);
            }
        }
        else
        {
            var hashTable = PoolInts.Rent(HASH_TABLESIZE);
            try
            {
                packedLength = encode(hashTable.AsSpan(0, HASH_TABLESIZE), input, outputBuffer.Slice(8));
            }
            finally
            {
                PoolInts.Return(hashTable);
            }
        }

        BitConverter.TryWriteBytes(outputBuffer,          input.Length);
        BitConverter.TryWriteBytes(outputBuffer.Slice(4), packedLength);

        if (packedLength > 0)
            return outputBuffer.Slice(0, packedLength + 8);

        input.CopyTo(outputBuffer.Slice(8));
        return outputBuffer.Slice(0, input.Length + 8);
    }

    #endregion
}