using System.Collections.Concurrent;
using System.Diagnostics;
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
    private const int Processes = 200;
    private const int InitThreads = 16;

    public static void Run()
    {
        Console.WriteLine($"threads = {ThreadCount}, rounds = {Rounds}, cores = {Environment.ProcessorCount}");
        Console.WriteLine();
        Console.WriteLine("| 대상 | 중복 생성 라운드 | 라운드당 최대 인스턴스 | 예외 |");
        Console.WriteLine("|---|---:|---:|---:|");

        // 여기서는 프로세스 최초 초기화 경쟁이 첫 라운드에 한 번만 열린다. 그 경쟁은 아래 MeasureInit 이 프로세스를 새로 띄워 확인한다.
        Measure("FastMember, allowNonPublic 혼합", (type, thread) => TypeAccessor.Create(type, thread % 2 == 0), distinctKeys: 2);
        Measure("FastMember, 같은 옵션", (type, _) => TypeAccessor.Create(type), distinctKeys: 1);
        Measure("ReflectProbe, 범위 2개 혼합", (type, thread) => TypeProbe.Of(type, thread % 2 == 0 ? ProbeScope.All : ProbeScope.Public | ProbeScope.Instance), distinctKeys: 2);
        Measure("ReflectProbe, 같은 범위", (type, _) => TypeProbe.Of(type), distinctKeys: 1);

        Console.WriteLine();
        Console.WriteLine($"프로세스 최초 생성 (새 프로세스 {Processes}개 × {InitThreads}스레드, 서로 다른 타입 8개)");
        Console.WriteLine();
        Console.WriteLine("| 대상 | 예외가 난 프로세스 | 동적 어셈블리가 2개 이상인 프로세스 |");
        Console.WriteLine("|---|---:|---:|");
        MeasureInit("FastMember, allowNonPublic 혼합", "fm-mixed");
        MeasureInit("FastMember, 같은 옵션", "fm-same");
        MeasureInit("ReflectProbe, 범위 2개 혼합", "probe-mixed");
    }

    /// <summary>
    /// FastMember 의 동적 어셈블리(static 필드)는 프로세스에서 처음 접근자를 만들 때 한 번만 초기화된다.
    /// 락이 allowNonPublicAccessors 값마다 따로라 그 순간의 경쟁은 프로세스를 새로 띄워야 다시 볼 수 있다.
    /// </summary>
    private static void MeasureInit(string name, string mode)
    {
        int failed = 0, duplicated = 0;
        var messages = new Dictionary<string, int>();
        for (int i = 0; i < Processes; i++)
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, UseShellExecute = false };
            info.ArgumentList.Add("concurrency-init");
            info.ArgumentList.Add(mode);
            using Process process = Process.Start(info)!;
            string[] output = process.StandardOutput.ReadToEnd().Trim().Split('|');
            process.WaitForExit();

            if (int.Parse(output[0]) > 0)
            {
                failed++;
                messages[output[2]] = messages.GetValueOrDefault(output[2]) + 1;
            }
            if (int.Parse(output[1]) > 1)
                duplicated++;
        }

        Console.WriteLine($"| {name} | {failed} / {Processes} | {duplicated} / {Processes} |");
        foreach (var pair in messages)
            Console.WriteLine($"  - {pair.Value}회 {pair.Key}");
    }

    /// <summary>자식 프로세스 : "예외 수|동적 어셈블리 수|마지막 예외" 를 출력한다.</summary>
    public static void RunInit(string mode)
    {
        Type[] types = Enumerable.Range(0, 8).Select(_ => DynamicTypes.Create(fieldCount: 1)).ToArray();
        int errors = 0;
        string message = "";
        using var barrier = new Barrier(InitThreads);
        var threads = new Thread[InitThreads];

        for (int t = 0; t < InitThreads; t++)
        {
            int index = t;
            threads[t] = new Thread(() =>
            {
                Type type = types[index % types.Length];
                bool flag = index % 2 == 0;
                barrier.SignalAndWait();
                try
                {
                    switch (mode)
                    {
                        case "fm-mixed": TypeAccessor.Create(type, flag); break;
                        case "fm-same": TypeAccessor.Create(type); break;
                        case "probe-mixed": TypeProbe.Of(type, flag ? ProbeScope.All : ProbeScope.Public | ProbeScope.Instance); break;
                        default: throw new ArgumentException(mode);
                    }
                }
                catch (Exception e)
                {
                    Interlocked.Increment(ref errors);
                    message = e.GetType().Name;
                }
            });
            threads[t].Start();
        }

        foreach (Thread thread in threads)
            thread.Join();

        int assemblies = AppDomain.CurrentDomain.GetAssemblies().Count(a => a.GetName().Name == "FastMember_dynamic");
        Console.WriteLine($"{errors}|{assemblies}|{message}");
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
