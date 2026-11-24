using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using FastMember;

namespace ReflectProbe.Benchmarks;

// 14주 시나리오 3-a : 여러 스레드의 동시 히트 처리량. 동시 최초 생성의 중복 여부는 ConcurrencyCheck 가 확인한다.
// 작업 스레드는 미리 만들어 Barrier 로 출발 · 도착을 맞춘다. 수치는 "스레드 1개가 접근 1회에 쓴 시간" 이라
// 잠금 경합이 없으면 물리 코어 수(i3-12100F 는 4)까지 스레드가 늘어도 같게 유지된다.
// string 멤버를 읽어 박싱 할당(GC)이 섞이지 않게 한다.
// 실행 : dotnet run -c Release -- --filter *ConcurrentHit*
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory, BenchmarkLogicalGroupRule.ByParams)]
[CategoriesColumn]
[BaselineColumn]
public class ConcurrentHitBenchmarks
{
    private const int OpsPerThread = 200_000;

    private readonly HitModel _model = new();
    private readonly TypeAccessor _accessor = TypeAccessor.Create(typeof(HitModel));
    private readonly TypeProbe _probe = TypeProbe.Of(typeof(HitModel));

    private Barrier _barrier = null!;
    private Thread[] _threads = null!;
    private Action? _work;

    [Params(1, 4, 8)]
    public int Threads { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _barrier = new Barrier(Threads + 1);
        _threads = new Thread[Threads];
        for (int i = 0; i < Threads; i++)
        {
            _threads[i] = new Thread(Worker) { IsBackground = true };
            _threads[i].Start();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _work = null;
        _barrier.SignalAndWait();
        foreach (Thread thread in _threads)
            thread.Join();
        _barrier.Dispose();
    }

    private void Worker()
    {
        while (true)
        {
            _barrier.SignalAndWait();
            Action? work = _work;
            if (work == null)
                return;
            work();
            _barrier.SignalAndWait();
        }
    }

    private void Run(Action work)
    {
        _work = work;
        _barrier.SignalAndWait(); // 출발
        _barrier.SignalAndWait(); // 모두 도착
    }

    // ---------------- Held : 접근자를 들고 있는 경우 ----------------

    [Benchmark(Baseline = true, OperationsPerInvoke = OpsPerThread), BenchmarkCategory("Held")]
    public void FastMember_Held() => Run(() =>
    {
        for (int i = 0; i < OpsPerThread; i++)
            _ = _accessor[_model, nameof(HitModel.StringField)];
    });

    [Benchmark(OperationsPerInvoke = OpsPerThread), BenchmarkCategory("Held")]
    public void Probe_Held() => Run(() =>
    {
        for (int i = 0; i < OpsPerThread; i++)
            _ = _probe[_model, nameof(HitModel.StringField)];
    });

    // ---------------- Lookup : 호출마다 타입 캐시에서 찾는 경우 ----------------

    [Benchmark(Baseline = true, OperationsPerInvoke = OpsPerThread), BenchmarkCategory("Lookup")]
    public void FastMember_Lookup() => Run(() =>
    {
        for (int i = 0; i < OpsPerThread; i++)
            _ = TypeAccessor.Create(typeof(HitModel))[_model, nameof(HitModel.StringField)];
    });

    [Benchmark(OperationsPerInvoke = OpsPerThread), BenchmarkCategory("Lookup")]
    public void Probe_Lookup() => Run(() =>
    {
        for (int i = 0; i < OpsPerThread; i++)
            _ = TypeProbe.Of(typeof(HitModel))[_model, nameof(HitModel.StringField)];
    });
}
