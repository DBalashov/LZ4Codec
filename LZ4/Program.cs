using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace LZ4
{
    class Program
    {
        static readonly string Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        static void Main(string[] args)
        {
            var rng = new RNGCryptoServiceProvider();
            var files = Directory.EnumerateFiles(Path.Combine(Location, "test"), "*.*")
                                 .ToDictionary(Path.GetFileName, File.ReadAllBytes);

            {
                // pack speed
                var sw           = Stopwatch.StartNew();
                int unpackLength = 0;
                int packLength   = 0;
                for (var i = 0; i < 100; i++)
                {
                    foreach (var item in files)
                    {
                        var packed = item.Value.AsSpan().PackLZ4();
                        unpackLength += item.Value.Length;
                        packLength   += packed.Length;
                        //var unpacked = packed.UnpackLZ4();
                    }
                }
            
                // x32 - 583 MB/sec
                // x64 - 546 MB/sec
                Console.WriteLine("{0:F2}", unpackLength / 1_048_576.0 / sw.Elapsed.TotalSeconds);
            }
            
            {
                // unpack speed
                var sw           = Stopwatch.StartNew();
                int unpackLength = 0;
                int packLength   = 0;
                var packedFiles = files.ToDictionary(p => p.Key, p => p.Value.AsSpan().PackLZ4().ToArray());
                
                for (var i = 0; i < 100; i++)
                {
                    foreach (var item in packedFiles)
                    {
                        var unpacked = item.Value.AsSpan().UnpackLZ4();
                        unpackLength += unpacked.Length;
                        packLength   += item.Value.Length;
                    }
                }
            
                // x32 - 762 MB/sec
                // x64 - 800 MB/sec
                Console.WriteLine("{0:F2}", unpackLength / 1_048_576.0 / sw.Elapsed.TotalSeconds);
            }

            // var buff = new byte[3072];
            // rng.GetBytes(buff);
            // files.Add("random_small.bin", buff);
            //
            // buff = new byte[100_000];
            // rng.GetBytes(buff);
            // files.Add("random_big.bin", buff);
            //
            // foreach (var item in files)
            // {
            //     Console.WriteLine("{0}", item.Key);
            //
            //     var packed   = item.Value.PackLZ4();
            //     var unpacked = packed.UnpackLZ4();
            //
            //     if (!item.Value.SequenceEqual(unpacked))
            //         throw new InvalidDataException(item.Key);
            // }
        }
    }
}