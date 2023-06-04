using System;
using LZ4.Helpers;

namespace LZ4;

public static class Extenders_Public
{
    public static (int UnpackedLength, int PackedLength) LZ4GetLengths(this Span<byte> span) =>
        (span.LZ4UnpackedLength(),
         span.LZ4PackedLength());
}