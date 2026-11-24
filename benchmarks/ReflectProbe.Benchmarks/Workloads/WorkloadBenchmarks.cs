using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace ReflectProbe.Benchmarks.Workloads;

// 14주 시나리오 4 · 5 : 실제 워크로드. 문자열 조립 비용이 섞여 있어 히트 경로보다 차이가 작게 나온다.
// A (public 모델)  : Reflection · Reflection_Cached · FastMember · ReflectProbe
// B (private 원본) : Reflection · Reflection_Cached · ReflectProbe  (FastMember 는 private 필드를 읽지 못한다)
// GlobalSetup 에서 구현끼리 출력이 같은지 확인하고 다르면 예외를 던진다. FastMember 는 멤버가 이름순이라 줄을 정렬해 비교한다.
// 실행 : dotnet run -c Release -- --filter *Dump* *Watch*

internal static class OutputCheck
{
    public static void Same(string name, string expected, string actual)
    {
        if (Sort(expected) != Sort(actual))
            throw new InvalidOperationException($"{name} 의 출력이 다릅니다.\n--- 기준 ---\n{expected}\n--- {name} ---\n{actual}");
    }

    private static string Sort(string text) => string.Join('\n', text.Split('\n').Order(StringComparer.Ordinal));
}

[MemoryDiagnoser]
[BaselineColumn]
public class DumpPublicBenchmarks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

    private readonly ReflectionDumper _reflection = new(Flags);
    private readonly CachedReflectionDumper _cached = new(Flags);
    private readonly FastMemberDumper _fastMember = new();
    private readonly ProbeDumper _probe = new(ProbeScope.Public | ProbeScope.Instance);
    private object _target = null!;

    [Params("Small", "Large")]
    public string Sample { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        _target = Sample == "Small" ? WorkloadSamples.PublicSmall() : WorkloadSamples.PublicLarge();
        string expected = _reflection.Dump(_target);
        OutputCheck.Same(nameof(Reflection_Cached), expected, _cached.Dump(_target));
        OutputCheck.Same(nameof(FastMember), expected, _fastMember.Dump(_target));
        OutputCheck.Same(nameof(Probe), expected, _probe.Dump(_target));
    }

    [Benchmark(Baseline = true)]
    public string Reflection() => _reflection.Dump(_target);

    [Benchmark]
    public string Reflection_Cached() => _cached.Dump(_target);

    [Benchmark]
    public string FastMember() => _fastMember.Dump(_target);

    [Benchmark]
    public string Probe() => _probe.Dump(_target);
}

[MemoryDiagnoser]
[BaselineColumn]
public class DumpPrivateBenchmarks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private readonly ReflectionDumper _reflection = new(Flags);
    private readonly CachedReflectionDumper _cached = new(Flags);
    private readonly ProbeDumper _probe = new(ProbeScope.Public | ProbeScope.NonPublic | ProbeScope.Instance);
    private object _target = null!;

    [Params("Small", "Large")]
    public string Sample { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        _target = Sample == "Small" ? WorkloadSamples.PrivateSmall() : WorkloadSamples.PrivateLarge();
        string expected = _reflection.Dump(_target);
        OutputCheck.Same(nameof(Reflection_Cached), expected, _cached.Dump(_target));
        OutputCheck.Same(nameof(Probe), expected, _probe.Dump(_target));
    }

    [Benchmark(Baseline = true)]
    public string Reflection() => _reflection.Dump(_target);

    [Benchmark]
    public string Reflection_Cached() => _cached.Dump(_target);

    [Benchmark]
    public string Probe() => _probe.Dump(_target);
}

[MemoryDiagnoser]
[BaselineColumn]
public class WatchPublicBenchmarks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

    private readonly ReflectionWatcher _reflection = new(Flags);
    private readonly CachedReflectionWatcher _cached = new(Flags);
    private readonly FastMemberWatcher _fastMember = new();
    private readonly ProbeWatcher _probe = new(ProbeScope.Public | ProbeScope.Instance);
    private readonly StringBuilder _sb = new();
    private PublicEnemy[] _enemies = null!;

    [Params(100, 1000)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _enemies = Enumerable.Range(0, N).Select(_ => new PublicEnemy()).ToArray();
        string expected = Output(Reflection);
        OutputCheck.Same(nameof(Reflection_Cached), expected, Output(Reflection_Cached));
        OutputCheck.Same(nameof(FastMember), expected, Output(FastMember));
        OutputCheck.Same(nameof(Probe), expected, Output(Probe));
    }

    private string Output(Func<int> run)
    {
        run();
        return _sb.ToString();
    }

    [Benchmark(Baseline = true)]
    public int Reflection()
    {
        _sb.Clear();
        foreach (PublicEnemy enemy in _enemies)
            _reflection.Watch(enemy, _sb);
        return _sb.Length;
    }

    [Benchmark]
    public int Reflection_Cached()
    {
        _sb.Clear();
        foreach (PublicEnemy enemy in _enemies)
            _cached.Watch(enemy, _sb);
        return _sb.Length;
    }

    [Benchmark]
    public int FastMember()
    {
        _sb.Clear();
        foreach (PublicEnemy enemy in _enemies)
            _fastMember.Watch(enemy, _sb);
        return _sb.Length;
    }

    [Benchmark]
    public int Probe()
    {
        _sb.Clear();
        foreach (PublicEnemy enemy in _enemies)
            _probe.Watch(enemy, _sb);
        return _sb.Length;
    }
}

[MemoryDiagnoser]
[BaselineColumn]
public class WatchPrivateBenchmarks
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private readonly ReflectionWatcher _reflection = new(Flags);
    private readonly CachedReflectionWatcher _cached = new(Flags);
    private readonly ProbeWatcher _probe = new(ProbeScope.Public | ProbeScope.NonPublic | ProbeScope.Instance);
    private readonly StringBuilder _sb = new();
    private PrivateEnemy[] _enemies = null!;

    [Params(100, 1000)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _enemies = Enumerable.Range(0, N).Select(_ => new PrivateEnemy()).ToArray();
        string expected = Output(Reflection);
        OutputCheck.Same(nameof(Reflection_Cached), expected, Output(Reflection_Cached));
        OutputCheck.Same(nameof(Probe), expected, Output(Probe));
    }

    private string Output(Func<int> run)
    {
        run();
        return _sb.ToString();
    }

    [Benchmark(Baseline = true)]
    public int Reflection()
    {
        _sb.Clear();
        foreach (PrivateEnemy enemy in _enemies)
            _reflection.Watch(enemy, _sb);
        return _sb.Length;
    }

    [Benchmark]
    public int Reflection_Cached()
    {
        _sb.Clear();
        foreach (PrivateEnemy enemy in _enemies)
            _cached.Watch(enemy, _sb);
        return _sb.Length;
    }

    [Benchmark]
    public int Probe()
    {
        _sb.Clear();
        foreach (PrivateEnemy enemy in _enemies)
            _probe.Watch(enemy, _sb);
        return _sb.Length;
    }
}
