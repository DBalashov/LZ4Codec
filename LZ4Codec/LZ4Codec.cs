using System;

namespace LZ4
{
    public static class LZ4Codec
    {
        internal static readonly LZ4ServiceBase service = new LZ4Service64(); // new LZ4Service64()

        public static Span<byte> PackLZ4(this Span<byte> inputBuffer) => service.Encode(inputBuffer);

        public static Span<byte> UnpackLZ4(this Span<byte> inputBuffer) => service.Decode(inputBuffer);
    }
}