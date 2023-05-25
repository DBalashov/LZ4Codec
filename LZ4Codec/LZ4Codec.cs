using System;

namespace LZ4;

public static class LZ4Codec
{
    internal static LZ4ServiceBase service = new LZ4Service64();

    public static Span<byte> PackLZ4(this Span<byte> inputBuffer) => service.Encode(inputBuffer);

    public static byte[] UnpackLZ4(this Span<byte> inputBuffer) => service.Decode(inputBuffer);

    public static void Use64Version() => service = new LZ4Service64();

    public static void Use32Version() => service = new LZ4Service32();
}