using System;
using System.IO;

namespace LZ4;

public sealed class LZ4CompressStream : Stream
{
    readonly Stream stm;
    readonly bool   closeParentStream;

    readonly byte[] accum = new byte[LZ4ServiceBase.LZ4_64KLIMIT - 1];
    int             accumOffset;

    public LZ4CompressStream(Stream stm, bool closeParentStream = true)
    {
        this.stm               = stm;
        this.closeParentStream = closeParentStream;
    }

    public override int  Read(byte[]    buffer, int        offset, int count) => throw new NotSupportedException("Read not supported");
    public override long Seek(long      offset, SeekOrigin origin) => throw new NotSupportedException("Seek not supported");
    public override void SetLength(long value) => throw new NotSupportedException("SetLength not supported");

    public override void Write(byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            var rest        = accum.Length - accumOffset;
            var writeLength = count <= rest ? count : rest;

            buffer.AsSpan(offset, writeLength).CopyTo(accum.AsSpan(accumOffset));

            offset      += writeLength;
            accumOffset += writeLength;
            count       -= writeLength;

            if (accumOffset == accum.Length)
                Flush();
        }
    }

    public override void Flush()
    {
        if (accumOffset > 0)
            stm.Write(accum.AsSpan(0, accumOffset).PackLZ4());
        accumOffset = 0;
    }

    public override bool CanRead  => false;
    public override bool CanSeek  => false;
    public override bool CanWrite => true;
    public override long Length   => stm.Length;

    public override long Position
    {
        get => stm.Position;
        set => throw new NotSupportedException("Position.Set not supported");
    }

    protected override void Dispose(bool disposing)
    {
        Flush();

        base.Dispose(disposing);
        if (closeParentStream)
            stm.Dispose();
    }
}