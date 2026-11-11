using System.Reflection;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using FastMember;
using ReflectProbe;

namespace ReflectProbe.Benchmarks;

// 12주 : 생성자 팩토리 · 메서드 Invoker.
// 5주 ReflectionBenchmark 의 ConstructorBenchmarks · MethodCallEmitBenchmarks 와 같은 Enemy · 같은 메서드 이름으로 비교한다.
// 실행 : dotnet run -c Release -- --filter *ConstructorMethodBenchmarks*

public class Enemy
{
    private int _hp;
    private int _damage;

    public Enemy() { }

    public Enemy(int hp, int damage)
    {
        _hp = hp;
        _damage = damage;
    }

    public int Attack(int multiplier) => _damage * multiplier;

    // 기준용 : 인라이닝을 막아 "호출 한 번의 비용"이 측정되도록 한다 (5주와 같음)
    [MethodImpl(MethodImplOptions.NoInlining)]
    public int AttackNoInline(int multiplier) => _damage * multiplier;
}

public class ZeroAsBytesConfig : ManualConfig
{
    // 할당 0 을 '-' 대신 '0 B' 로 출력 (11주와 같음)
    public ZeroAsBytesConfig() => SummaryStyle = SummaryStyle.Default.WithZeroMetricValuesInContent();
}

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
[BaselineColumn]
[Config(typeof(ZeroAsBytesConfig))]
public class ConstructorMethodBenchmarks
{
    private readonly Type _enemyType = typeof(Enemy);
    private readonly Enemy _enemy = new(100, 50);

    private ConstructorInfo _enemyCtor = null!;
    private MethodInfo _attackMethod = null!;
    private TypeAccessor _accessor = null!;
    private TypeProbe _probe = null!;
    private TypeProbe<Enemy> _typedProbe = null!;
    private ProbeFactory _factory = null!;
    private ProbeInvoker _attack = null!;

    [GlobalSetup]
    public void Setup()
    {
        _enemyCtor = _enemyType.GetConstructor(new[] { typeof(int), typeof(int) })!;
        _attackMethod = _enemyType.GetMethod(nameof(Enemy.Attack))!;
        _accessor = TypeAccessor.Create(_enemyType);

        _probe = TypeProbe.Of(_enemyType);
        _typedProbe = TypeProbe.Of<Enemy>();
        _factory = _probe.GetFactory(typeof(int), typeof(int));
        _attack = _probe.GetInvoker(nameof(Enemy.Attack));
        _probe.CreateInstance();                       // 기본 팩토리 생성
        _probe.Invoke(_enemy, nameof(Enemy.Attack), 2); // 이름 캐시 채우기
    }

    // ---------------- NoArgs : 매개변수 없는 생성자 ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("NoArgs")]
    public Enemy New() => new Enemy();

    [Benchmark, BenchmarkCategory("NoArgs")]
    public object Activator_Type() => Activator.CreateInstance(_enemyType)!;

    [Benchmark, BenchmarkCategory("NoArgs")]
    public object FastMember_CreateNew() => _accessor.CreateNew();

    [Benchmark, BenchmarkCategory("NoArgs")]
    public object Probe_CreateInstance() => _probe.CreateInstance();

    [Benchmark, BenchmarkCategory("NoArgs")]
    public Enemy Probe_CreateInstance_T() => _typedProbe.CreateInstance();

    // ---------------- WithArgs : new Enemy(100, 50) ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("WithArgs")]
    public Enemy New_WithArgs() => new Enemy(100, 50);

    // 매 호출마다 생성자 탐색 + params 배열 + 박싱
    [Benchmark, BenchmarkCategory("WithArgs")]
    public object Activator_WithArgs() => Activator.CreateInstance(_enemyType, 100, 50)!;

    [Benchmark, BenchmarkCategory("WithArgs")]
    public object ConstructorInfo_Invoke_WithArgs() => _enemyCtor.Invoke(new object[] { 100, 50 });

    // Activator 와 같은 조건 : 인자 배열을 매 호출 생성
    [Benchmark, BenchmarkCategory("WithArgs")]
    public object ProbeFactory_WithArgs() => _factory(new object?[] { 100, 50 });

    // ---------------- Method : enemy.Attack(2) ----------------

    [Benchmark(Baseline = true), BenchmarkCategory("Method")]
    public int Direct_NoInline() => _enemy.AttackNoInline(2);

    [Benchmark, BenchmarkCategory("Method")]
    public int MethodInfo_Invoke() => (int)_attackMethod.Invoke(_enemy, new object[] { 2 })!;

    [Benchmark, BenchmarkCategory("Method")]
    public int ProbeInvoker_Call() => (int)_attack(_enemy, new object?[] { 2 })!;

    // 이름 → Invoker 캐시 조회까지 포함
    [Benchmark, BenchmarkCategory("Method")]
    public int Probe_Invoke_ByName() => (int)_probe.Invoke(_enemy, nameof(Enemy.Attack), 2)!;
}
