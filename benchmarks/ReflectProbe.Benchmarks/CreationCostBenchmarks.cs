using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using FastMember;

namespace ReflectProbe.Benchmarks;

// 14주 시나리오 2 : 최초 생성 비용. 반복마다 새 타입 TypeCount(100) 개를 만들어(측정 밖) 하나씩 처음 쓴다.
// Create   : 접근자를 만들기만 한다. DynamicMethod 의 JIT 는 첫 호출까지 미뤄지므로 실제 비용보다 작게 나온다.
// FirstGet : 만들고 필드 1개를 처음 읽는다. JIT 까지 포함 → 손익분기 N 은 이 값으로 계산한다.
//   N = (FirstGet 생성 비용 차이) / (히트 경로의 Reflection 1회 − 라이브러리 1회)
// 라이브러리 자신의 JIT · 정적 초기화는 워밍업 반복에서 끝난다. (앱 시작 직후 첫 호출의 비용이 아니다)
// 한 프로세스에서 반복을 많이 돌리지 않는다 : FastMember 는 모든 접근자를 한 동적 모듈에 쌓아서, 이미 만든 타입이 많을수록
// 다음 타입이 느려진다 (2,000개에서 타입당 약 4.6배). 반복을 쌓으면 2만 개 상태를 재게 된다.
// 그래서 워밍업 1회 · 측정 1회만 하고 프로세스를 10번 새로 띄운다 → "101 ~ 200 번째 타입" 의 비용이다.
// 실행 : dotnet run -c Release -- --filter *CreationCost*
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByParams)]
[CategoriesColumn]
[BaselineColumn]
[SimpleJob(RunStrategy.Monitoring, launchCount: 10, warmupCount: 1, iterationCount: 1)]
public class CreationCostBenchmarks
{
    private const int TypeCount = 100;
    private const ProbeScope PublicInstance = ProbeScope.Public | ProbeScope.Instance;

    private readonly Type[] _types = new Type[TypeCount];
    private readonly object[] _targets = new object[TypeCount];

    [Params(4, 16)]
    public int FieldCount { get; set; }

    [IterationSetup]
    public void CreateTypes()
    {
        for (int i = 0; i < TypeCount; i++)
        {
            _types[i] = DynamicTypes.Create(FieldCount);
            _targets[i] = Activator.CreateInstance(_types[i])!;
        }
    }

    // ---------------- Create ----------------

    // 참고 : 멤버 조회만. 접근 코드를 만들지 않는다.
    [Benchmark(Baseline = true, OperationsPerInvoke = TypeCount), BenchmarkCategory("Create")]
    public int Reflection_GetFields()
    {
        int sum = 0;
        foreach (Type type in _types)
            sum += type.GetFields(BindingFlags.Public | BindingFlags.Instance).Length;
        return sum;
    }

    [Benchmark(OperationsPerInvoke = TypeCount), BenchmarkCategory("Create")]
    public object? FastMember_Create()
    {
        object? last = null;
        foreach (Type type in _types)
            last = TypeAccessor.Create(type);
        return last;
    }

    [Benchmark(OperationsPerInvoke = TypeCount), BenchmarkCategory("Create")]
    public object? Probe_Of()
    {
        object? last = null;
        foreach (Type type in _types)
            last = TypeProbe.Of(type);
        return last;
    }

    // FastMember 와 수집 범위를 맞춘 것
    [Benchmark(OperationsPerInvoke = TypeCount), BenchmarkCategory("Create")]
    public object? Probe_Of_PublicInstance()
    {
        object? last = null;
        foreach (Type type in _types)
            last = TypeProbe.Of(type, PublicInstance);
        return last;
    }

    // ---------------- FirstGet ----------------

    [Benchmark(Baseline = true, OperationsPerInvoke = TypeCount), BenchmarkCategory("FirstGet")]
    public int Reflection_FirstGet()
    {
        int sum = 0;
        for (int i = 0; i < TypeCount; i++)
            sum += (int)_types[i].GetField("F0")!.GetValue(_targets[i])!;
        return sum;
    }

    [Benchmark(OperationsPerInvoke = TypeCount), BenchmarkCategory("FirstGet")]
    public int FastMember_FirstGet()
    {
        int sum = 0;
        for (int i = 0; i < TypeCount; i++)
            sum += (int)TypeAccessor.Create(_types[i])[_targets[i], "F0"];
        return sum;
    }

    [Benchmark(OperationsPerInvoke = TypeCount), BenchmarkCategory("FirstGet")]
    public int Probe_FirstGet()
    {
        int sum = 0;
        for (int i = 0; i < TypeCount; i++)
            sum += (int)TypeProbe.Of(_types[i])[_targets[i], "F0"]!;
        return sum;
    }

    [Benchmark(OperationsPerInvoke = TypeCount), BenchmarkCategory("FirstGet")]
    public int Probe_FirstGet_PublicInstance()
    {
        int sum = 0;
        for (int i = 0; i < TypeCount; i++)
            sum += (int)TypeProbe.Of(_types[i], PublicInstance)[_targets[i], "F0"]!;
        return sum;
    }
}
