using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LZ4;
using NUnit.Framework;

namespace LZ4Codec.Tests;

public abstract class BaseTest
{
    internal LZ4ServiceBase service;

    /// <summary> low entropy data </summary>
    protected Dictionary<string, byte[]> dataCompressable = new();

    /// <summary> high entropy data </summary>
    protected readonly Dictionary<string, byte[]> dataUncompressable = new();

    [OneTimeSetUp]
    public virtual void OneTimeSetup()
    {
        var buff = RandomNumberGenerator.GetBytes(64);
        dataUncompressable.Add("random_very_small.bin", buff);

        buff = RandomNumberGenerator.GetBytes(4096);
        dataUncompressable.Add("random_small.bin", buff);

        buff = RandomNumberGenerator.GetBytes(128 * 1024);
        dataUncompressable.Add("random_big.bin", buff);

        var dataPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "data");
        dataCompressable = Directory.EnumerateFiles(dataPath, "*.*").ToDictionary(Path.GetFileName, File.ReadAllBytes);
    }

    [Test]
    public void PackUnpackCompressable()
    {
        foreach (var data in dataCompressable)
        {
            var copyOfOriginal = data.Value.ToArray();

            var packed = service.Encode(copyOfOriginal);
            Assert.True(data.Value.SequenceEqual(copyOfOriginal));
            Assert.True(packed.Length > 0);
            Assert.True(packed.Length <= copyOfOriginal.Length);

            var unpacked = service.Decode(packed);
            Assert.IsTrue(unpacked.Length > 0);
            Assert.True(unpacked.SequenceEqual(copyOfOriginal));
        }
    }

    [Test]
    public void PackUnpackUncompressable()
    {
        foreach (var data in dataUncompressable)
        {
            var copyOfOriginal = data.Value.ToArray();

            var packed = service.Encode(copyOfOriginal);
            Assert.True(data.Value.SequenceEqual(copyOfOriginal));
            Assert.True(packed.Length     > 0);
            Assert.True(packed.Length - 8 <= copyOfOriginal.Length);

            var unpacked = service.Decode(packed);
            Assert.IsTrue(unpacked.Length > 0);
            Assert.True(unpacked.SequenceEqual(copyOfOriginal));
        }
    }

    [Test]
    public void StreamCompress_Compressable()
    {
        foreach (var data in dataCompressable)
        {
            var copyOfOriginal = data.Value.ToArray();

            using var stm = new MemoryStream();
            using var pk  = new LZ4CompressStream(stm);

            while (copyOfOriginal.Any())
            {
                var part = copyOfOriginal.Take(Random.Shared.Next(data.Value.Length / 5, data.Value.Length / 3)).ToArray();
                if (part.Length == 0) break;
                pk.Write(part);

                copyOfOriginal = copyOfOriginal.Skip(part.Length).ToArray();
            }

            pk.Flush();

            var packed = stm.ToArray();
            Assert.True(packed.Length <= data.Value.Length);

            var unpacked = service.Decode(packed);
            Assert.IsNotNull(unpacked);
            Assert.IsTrue(unpacked.Length > 0);
            Assert.True(unpacked.SequenceEqual(data.Value));
        }
    }

    [Test]
    public void StreamDecompress_Compressable()
    {
        foreach (var data in dataCompressable)
        {
            var copyOfOriginal = data.Value.ToArray();

            using var stm = new MemoryStream();
            using var pk  = new LZ4CompressStream(stm);

            while (copyOfOriginal.Any())
            {
                var part = copyOfOriginal.Take(Random.Shared.Next(data.Value.Length / 5, data.Value.Length / 3)).ToArray();
                if (part.Length == 0) break;
                pk.Write(part);

                copyOfOriginal = copyOfOriginal.Skip(part.Length).ToArray();
            }

            pk.Flush();

            using var st    = new MemoryStream(stm.ToArray());
            using var unpk  = new LZ4DecompressStream(st);
            var       final = new MemoryStream();
            while (true)
            {
                var buff = new byte[Random.Shared.Next(100, 100000)];
                var read = unpk.Read(buff, 0, buff.Length);
                if (read == 0) break;

                final.Write(buff, 0, read);
                if (read < buff.Length) break;
            }

            Assert.True(final.ToArray().SequenceEqual(data.Value));
        }
    }

    [Test]
    public void EmptyPack()
    {
        var packed = service.Encode(Array.Empty<byte>());
        Assert.IsTrue(packed.Length == 0);
    }

    [Test]
    public void EmptyUnpack()
    {
        var unpacked = service.Decode(Array.Empty<byte>());
        Assert.IsTrue(unpacked.Length == 0);
    }

    [Test]
    public void InvalidUnpack()
    {
        var buff = RandomNumberGenerator.GetBytes(1024);
        BitConverter.TryWriteBytes(buff.AsSpan(),          -2);
        BitConverter.TryWriteBytes(buff.AsSpan().Slice(4), -3);
        Assert.Throws<InvalidOperationException>(() => service.Decode(buff));

        BitConverter.TryWriteBytes(buff.AsSpan(),          123);
        BitConverter.TryWriteBytes(buff.AsSpan().Slice(4), 444);
        Assert.Throws<InvalidOperationException>(() => service.Decode(buff));
    }
}