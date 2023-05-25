using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using LZ4;

var summary = BenchmarkRunner.Run<MainTest>();

[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
[WarmupCount(3)]
[IterationCount(3)]
[MemoryDiagnoser]
[Config(typeof(Config))]
public class MainTest
{
    readonly string            Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    Dictionary<string, byte[]> rawFiles;
    Dictionary<string, byte[]> packedFiles;

    [Params("___1KB", "__16KB", "_128KB", "_512KB", "1024KB")]
    public string FileName { get; set; }

    [Params(32, 64)]
    public int Bitness { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        rawFiles = Directory.EnumerateFiles(Path.Combine(Location, "data"), "*.*")
                            .ToDictionary(Path.GetFileNameWithoutExtension, File.ReadAllBytes);
        packedFiles = rawFiles.ToDictionary(p => p.Key, p => p.Value.AsSpan().PackLZ4().ToArray());
    }

    [Benchmark]
    public int Pack()
    {
        if (Bitness == 32) LZ4Codec.Use32Version();
        else LZ4Codec.Use64Version();
        return rawFiles[FileName].AsSpan().PackLZ4().Length;
    }

    [Benchmark]
    public int Unpack()
    {
        if (Bitness == 32) LZ4Codec.Use32Version();
        else LZ4Codec.Use64Version();
        return packedFiles[FileName].AsSpan().UnpackLZ4().Length;
    }

    #region Config

    class Config : ManualConfig
    {
        public Config() => Orderer = new CustomOrderer();

        private class CustomOrderer : IOrderer
        {
            public IEnumerable<BenchmarkCase> GetExecutionOrder(ImmutableArray<BenchmarkCase> benchmarksCase, IEnumerable<BenchmarkLogicalGroupRule> order = null) =>
                benchmarksCase.OrderBy(p => p.Descriptor.WorkloadMethod.Name)
                              .ThenBy(p => (string) p.Parameters["FileName"])
                              .ThenBy(p => (int) p.Parameters["Bitness"]);

            public IEnumerable<BenchmarkCase> GetSummaryOrder(ImmutableArray<BenchmarkCase> benchmarksCase, Summary summary) =>
                benchmarksCase.OrderBy(p => p.Descriptor.WorkloadMethod.Name)
                              .ThenBy(p => (string) p.Parameters["FileName"])
                              .ThenBy(p => (int) p.Parameters["Bitness"]);

            public string GetHighlightGroupKey(BenchmarkCase benchmarkCase) => null;

            public string GetLogicalGroupKey(ImmutableArray<BenchmarkCase> allBenchmarksCases, BenchmarkCase benchmarkCase) =>
                benchmarkCase.Job.DisplayInfo + "_" + benchmarkCase.Parameters.DisplayInfo;

            public IEnumerable<IGrouping<string, BenchmarkCase>> GetLogicalGroupOrder(IEnumerable<IGrouping<string, BenchmarkCase>> logicalGroups,
                                                                                      IEnumerable<BenchmarkLogicalGroupRule>        order = null) =>
                logicalGroups.OrderBy(it => it.Key);

            public bool SeparateLogicalGroups => false;
        }
    }

    #endregion
}