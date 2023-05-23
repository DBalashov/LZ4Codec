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