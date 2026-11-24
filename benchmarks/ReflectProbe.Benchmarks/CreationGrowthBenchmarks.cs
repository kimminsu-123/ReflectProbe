using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using FastMember;

namespace ReflectProbe.Benchmarks;

// 시나리오 2 의 누적 조건 : 같은 라이브러리로 이미 ExistingTypes 개를 쓴 프로세스에서 다음 타입의 최초 생성 + 첫 읽기 비용.
// 기본 비용은 CreationCostBenchmarks 로 본다. 한 앱이 접근자를 수천 개 만드는 경우는 드물어 두 결과를 한 표에 섞지 않는다.
// 대상 타입은 세 벤치 모두 같은 수만큼 만들어 둔다 → Reflection 행은 "대상 타입이 늘어난 것 자체" 의 영향을 보는 대조군이다.
// 워밍업 100개가 더 쌓이므로 실제로 재는 것은 ExistingTypes + (101 ~ 200) 번째 타입이다.
// 실행 : dotnet run -c Release -- --filter *CreationGrowth*
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[BaselineColumn]
[SimpleJob(RunStrategy.Monitoring, launchCount: 10, warmupCount: 1, iterationCount: 1)]
public class CreationGrowthBenchmarks
{
    private const int TypeCount = 100;
    private const int FieldCount = 4;

    private readonly Type[] _types = new Type[TypeCount];
    private readonly object[] _targets = new object[TypeCount];

    [Params(0, 1000, 2000)]
    public int ExistingTypes { get; set; }

    [GlobalSetup(Target = nameof(Reflection_FirstGet))]
    public void FillReflection() => Fill(ReflectionFirstGet);

    [GlobalSetup(Target = nameof(FastMember_FirstGet))]
    public void FillFastMember() => Fill(FastMemberFirstGet);

    [GlobalSetup(Target = nameof(Probe_FirstGet))]
    public void FillProbe() => Fill(ProbeFirstGet);

    [IterationSetup]
    public void CreateTypes()
    {
        for (int i = 0; i < TypeCount; i++)
        {
            _types[i] = DynamicTypes.Create(FieldCount);
            _targets[i] = Activator.CreateInstance(_types[i])!;
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = TypeCount)]
    public int Reflection_FirstGet()
    {
        int sum = 0;
        for (int i = 0; i < TypeCount; i++)
            sum += ReflectionFirstGet(_types[i], _targets[i]);
        return sum;
    }

    [Benchmark(OperationsPerInvoke = TypeCount)]
    public int FastMember_FirstGet()
    {
        int sum = 0;
        for (int i = 0; i < TypeCount; i++)
            sum += FastMemberFirstGet(_types[i], _targets[i]);
        return sum;
    }

    [Benchmark(OperationsPerInvoke = TypeCount)]
    public int Probe_FirstGet()
    {
        int sum = 0;
        for (int i = 0; i < TypeCount; i++)
            sum += ProbeFirstGet(_types[i], _targets[i]);
        return sum;
    }

    private void Fill(Func<Type, object, int> firstGet)
    {
        for (int i = 0; i < ExistingTypes; i++)
        {
            Type type = DynamicTypes.Create(FieldCount);
            firstGet(type, Activator.CreateInstance(type)!);
        }
    }

    private static int ReflectionFirstGet(Type type, object target) => (int)type.GetField("F0")!.GetValue(target)!;

    private static int FastMemberFirstGet(Type type, object target) => (int)TypeAccessor.Create(type)[target, "F0"];

    private static int ProbeFirstGet(Type type, object target) => (int)TypeProbe.Of(type)[target, "F0"]!;
}
