using System.Collections.Concurrent;
using FastMember;

namespace ReflectProbe.Benchmarks;

/// <summary>
/// 14주 시나리오 3-b : 동시 최초 생성. 시간이 아니라 "같은 타입에 접근자가 몇 개 만들어졌는지 · 예외가 났는지" 를 센다.
/// 스레드를 Barrier 로 세워 아직 쓰지 않은 타입을 동시에 처음 요청한다. 결과는 경쟁 타이밍에 따라 실행마다 다를 수 있다.
/// 실행 : dotnet run -c Release -- concurrency
/// </summary>
internal static class ConcurrencyCheck
{
    private const int ThreadCount = 32;
    private const int Rounds = 500;

    public static void Run()
    {
        Console.WriteLine($"threads = {ThreadCount}, rounds = {Rounds}, cores = {Environment.ProcessorCount}");
        Console.WriteLine();
        Console.WriteLine("| 대상 | 중복 생성 라운드 | 라운드당 최대 인스턴스 | 예외 |");
        Console.WriteLine("|---|---:|---:|---:|");

        // 혼합을 먼저 : FastMember 동적 어셈블리의 프로세스 최초 초기화 경쟁까지 포함한다.
        Measure("FastMember, allowNonPublic 혼합", (type, thread) => TypeAccessor.Create(type, thread % 2 == 0), distinctKeys: 2);
        Measure("FastMember, 같은 옵션", (type, _) => TypeAccessor.Create(type), distinctKeys: 1);
        Measure("ReflectProbe, 범위 2개 혼합", (type, thread) => TypeProbe.Of(type, thread % 2 == 0 ? ProbeScope.All : ProbeScope.Public | ProbeScope.Instance), distinctKeys: 2);
        Measure("ReflectProbe, 같은 범위", (type, _) => TypeProbe.Of(type), distinctKeys: 1);
    }

    // distinctKeys : 중복이 없을 때 나와야 하는 인스턴스 수 (캐시 키 종류)
    private static void Measure(string name, Func<Type, int, object> create, int distinctKeys)
    {
        int duplicatedRounds = 0, maxInstances = 0;
        var exceptions = new ConcurrentBag<Exception>();

        for (int round = 0; round < Rounds; round++)
        {
            Type type = DynamicTypes.Create(fieldCount: 8);
            var results = new object?[ThreadCount];
            using var barrier = new Barrier(ThreadCount);
            var threads = new Thread[ThreadCount];

            for (int t = 0; t < ThreadCount; t++)
            {
                int index = t;
                threads[t] = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    try
                    {
                        results[index] = create(type, index);
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                });
                threads[t].Start();
            }

            foreach (Thread thread in threads)
                thread.Join();

            int instances = results.Where(r => r != null).Distinct(ReferenceEqualityComparer.Instance).Count();
            maxInstances = Math.Max(maxInstances, instances);
            if (instances > distinctKeys)
                duplicatedRounds++;
        }

        Console.WriteLine($"| {name} | {duplicatedRounds} / {Rounds} | {maxInstances} (정상 {distinctKeys}) | {exceptions.Count} |");
        foreach (var group in exceptions.GroupBy(e => e.GetType().Name + ": " + e.Message))
            Console.WriteLine($"  - {group.Count()}회 {group.Key}");
    }
}
