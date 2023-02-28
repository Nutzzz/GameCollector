using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using GameCollector.Common;

namespace GameCollector.Benchmarks;

[MemoryDiagnoser]
public class LinqBenchmarks
{
    public enum EnumerableType
    {
        Array,
        List,
    }

    private IEnumerable<Result<string>> _results = null!;
    private static readonly Consumer Consumer = new();

    [ParamsAllValues]
    public EnumerableType Type { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var enumerable = Enumerable.Range(0, 100).Select(_ => Result.FromGame(""));

        _results = Type switch
        {
            EnumerableType.Array => enumerable.ToArray(),
            EnumerableType.List => enumerable.ToList(),
            _ => throw new Exception(),
        };
    }

    [Benchmark(Baseline = true)]
    public void WithLinq()
    {
        UseLinq(_results).Consume(Consumer);
    }

    [Benchmark]
    public void WithForLoop()
    {
        UseForLoop(_results).Consume(Consumer);
    }

    private static IEnumerable<string> UseLinq(IEnumerable<Result<string>> results)
    {
        return results
            .Select(result => result.Game)
            .Where(game => game is not null)
            .Select(game => game!);
    }

    private static IEnumerable<string> UseForLoop(IEnumerable<Result<string>> results)
    {
        foreach (var result in results)
        {
            if (result.Game is not null)
            {
                yield return result.Game;
            }
        }
    }
}
