using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using LZ4;

var summary = BenchmarkRunner.Run<MainTest>();

[SimpleJob(RuntimeMoniker.Net70, baseline: true)]
//[SimpleJob(RuntimeMoniker.NativeAot70)]
[WarmupCount(3)]
[IterationCount(3)]
[MemoryDiagnoser]
[Config(typeof(Config))]
public class MainTest
{
    readonly string            Location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    Dictionary<string, byte[]> rawFiles;
    Dictionary<string, byte[]> packedFiles;

    [Params(1, 16, 128, 512, 1024)]
    public int FileSize { get; set; }

    //[Params(32, 64)]
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
        return rawFiles[FileSize.ToString()].AsSpan().PackLZ4().Length;
    }

    [Benchmark]
    public int Unpack()
    {
        if (Bitness == 32) LZ4Codec.Use32Version();
        else LZ4Codec.Use64Version();
        return packedFiles[FileSize.ToString()].AsSpan().UnpackLZ4().Length;
    }

    #region Config

    class Config : ManualConfig
    {
        public Config()
        {
            AddColumn(new SpeedColumn("Avg MB/sec"));
            Orderer = new CustomOrderer();
        }

        private class CustomOrderer : IOrderer
        {
            public IEnumerable<BenchmarkCase> GetExecutionOrder(ImmutableArray<BenchmarkCase> benchmarksCase, IEnumerable<BenchmarkLogicalGroupRule> order = null) =>
                benchmarksCase.OrderBy(p => p.Descriptor.WorkloadMethod.Name)
                              .ThenBy(p => (int) p.Parameters["Bitness"])
                              .ThenBy(p => (int) p.Parameters["FileSize"]);

            public IEnumerable<BenchmarkCase> GetSummaryOrder(ImmutableArray<BenchmarkCase> benchmarksCase, Summary summary) =>
                benchmarksCase.OrderBy(p => p.Descriptor.WorkloadMethod.Name)
                              .ThenBy(p => (int) p.Parameters["Bitness"])
                              .ThenBy(p => (int) p.Parameters["FileSize"]);

            public string GetHighlightGroupKey(BenchmarkCase benchmarkCase) => null;

            public string GetLogicalGroupKey(ImmutableArray<BenchmarkCase> allBenchmarksCases, BenchmarkCase benchmarkCase) =>
                benchmarkCase.Job.DisplayInfo + "_" + benchmarkCase.Parameters.DisplayInfo;

            public IEnumerable<IGrouping<string, BenchmarkCase>> GetLogicalGroupOrder(IEnumerable<IGrouping<string, BenchmarkCase>> logicalGroups,
                                                                                      IEnumerable<BenchmarkLogicalGroupRule>        order = null) =>
                logicalGroups.OrderBy(it => it.Key);

            public bool SeparateLogicalGroups => false;
        }
    }


    public class SpeedColumn : IColumn
    {
        public string Id         { get; }
        public string ColumnName { get; }

        public SpeedColumn(string columnName)
        {
            ColumnName = columnName;
            Id         = nameof(TagColumn) + "." + ColumnName;
        }

        public bool   IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
        public string GetValue(Summary  summary, BenchmarkCase benchmarkCase) => GetValue(summary, benchmarkCase, SummaryStyle.Default);

        public          bool           IsAvailable(Summary summary) => true;
        public          bool           AlwaysShow                   => true;
        public          ColumnCategory Category                     => ColumnCategory.Custom;
        public          int            PriorityInCategory           => 0;
        public          bool           IsNumeric                    => false;
        public          UnitType       UnitType                     => UnitType.Dimensionless;
        public          string         Legend                       => "Speed in MB/sec";
        public override string         ToString()                   => ColumnName;

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var r       = summary[benchmarkCase].ResultStatistics;
            var bytes   = ((int) benchmarkCase.Parameters["FileSize"] / 1024.0);
            var seconds = r.Mean / 1_000_000_000;
            return (bytes / seconds).ToString("F2", CultureInfo.InvariantCulture);
        }
    }

    #endregion
}