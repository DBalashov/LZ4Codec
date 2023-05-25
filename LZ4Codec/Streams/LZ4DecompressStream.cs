using System;
using System.IO;

namespace LZ4;

public sealed class LZ4DecompressStream : Stream
{
    readonly Stream stm;
    readonly bool   closeParentStream;

    readonly byte[] header             = new byte[8];
    readonly byte[] currentBlockPacked = new byte[LZ4ServiceBase.LZ4_64KLIMIT - 1 + 8];

    byte[] currentBlock       = Array.Empty<byte>();
    int    currentBlockOffset = 0;

    public LZ4DecompressStream(Stream stm, bool closeParentStream = true)
    {
        this.stm               = stm;
        this.closeParentStream = closeParentStream;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var initialOffset = offset;
        while (count > 0)
        {
            var rest = currentBlock.Length - currentBlockOffset;
            if (rest == 0)
            {
                if (!nextBlock()) break;
            }

            rest = currentBlock.Length - currentBlockOffset;

            var writeLength = count <= rest ? count : rest;
            currentBlock.AsSpan(currentBlockOffset, writeLength).CopyTo(buffer.AsSpan(offset, writeLength));

            count              -= writeLength;
            currentBlockOffset += writeLength;
            offset             += writeLength;
        }

        return offset - initialOffset;
    }

    bool nextBlock()
    {
        var read = stm.Read(header, 0, 8);

        if (read == 0) return false;

        if (read < 8)
            throw new InvalidDataException("packed data header invalid (truncated)");

        var unpackedLength = header.AsSpan().LZ4UnpackedLength();
        var packedLength   = header.AsSpan().LZ4PackedLength();

        header.AsSpan(0, header.Length).CopyTo(currentBlockPacked.AsSpan(0, 8));
        read = stm.Read(currentBlockPacked, 8, packedLength);
        if (read < packedLength)
            throw new InvalidDataException("packed data invalid (truncated)");

        currentBlock = currentBlockPacked.AsSpan(0, packedLength + 8).UnpackLZ4();
        if (currentBlock.Length != unpackedLength)
            throw new InvalidDataException("currentBlock.Length!=unpackedLength");

        currentBlockOffset = 0;
        return true;
    }

    public override void Write(byte[]   buffer, int        offset, int count) => throw new NotSupportedException("Write not supported");
    public override long Seek(long      offset, SeekOrigin origin) => throw new NotSupportedException("Seek not supported");
    public override void SetLength(long value) => throw new NotSupportedException("SetLength not supported");

    public override void Flush()
    {
    }

    public override bool CanRead  => true;
    public override bool CanSeek  => false;
    public override bool CanWrite => false;
    public override long Length   => stm.Length;

    public override long Position
    {
        get => stm.Position;
        set => throw new NotSupportedException("Position.Set not supported");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (closeParentStream)
            stm.Dispose();
    }
}