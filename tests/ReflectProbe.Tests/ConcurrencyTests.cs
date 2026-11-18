namespace ReflectProbe.Tests;

#pragma warning disable CS0169, CS0649 // 리플렉션으로만 접근하는 필드

// 각 테스트가 "처음" 상태에서 시작하도록 다른 테스트가 쓰지 않는 전용 타입을 쓴다 (캐시는 프로세스 전역).
internal class ScopeRaceTarget
{
    private int _a;
    public int B;
    private static int _c;
    public static int D;
}

internal class GetterRaceTarget
{
    private int _f0, _f1, _f2, _f3, _f4, _f5, _f6, _f7;
}

internal class HitTarget
{
    private int _value;
    public string Label { get; set; } = "";
}

#pragma warning restore CS0169, CS0649

public class ConcurrencyTests
{
    private const int ThreadCount = 32;

    /// <summary>스레드를 모두 세워 둔 뒤 동시에 출발시키고 결과를 모은다.</summary>
    private static T[] RunAtOnce<T>(Func<int, T> work)
    {
        var results = new T[ThreadCount];
        var errors = new Exception?[ThreadCount];
        using var barrier = new Barrier(ThreadCount);

        Thread[] threads = Enumerable.Range(0, ThreadCount).Select(i => new Thread(() =>
        {
            barrier.SignalAndWait();
            try { results[i] = work(i); }
            catch (Exception e) { errors[i] = e; }
        })).ToArray();

        foreach (Thread thread in threads) thread.Start();
        foreach (Thread thread in threads) thread.Join();

        Assert.All(errors, Assert.Null);
        return results;
    }

    [Fact]
    public void Of_DifferentScopesConcurrently_OneInstancePerScope()
    {
        ProbeScope[] scopes =
        {
            ProbeScope.All,
            ProbeScope.Public | ProbeScope.Instance,
            ProbeScope.NonPublic | ProbeScope.Instance,
            ProbeScope.Public | ProbeScope.Static,
        };

        // 같은 범위를 Of(Type) 과 Of<T>() 로 섞어 부른다 → 둘 다 같은 캐시 항목.
        TypeProbe[] results = RunAtOnce(i =>
        {
            ProbeScope scope = scopes[i % scopes.Length];
            return i % 2 == 0 ? TypeProbe.Of(typeof(ScopeRaceTarget), scope) : TypeProbe.Of<ScopeRaceTarget>(scope);
        });

        for (int s = 0; s < scopes.Length; s++)
        {
            TypeProbe[] sameScope = results.Where((_, i) => i % scopes.Length == s).ToArray();
            Assert.All(sameScope, r => Assert.Same(sameScope[0], r));
            Assert.Equal(scopes[s], sameScope[0].Scope);
        }

        Assert.Equal(scopes.Length, results.Distinct().Count());
        Assert.Equal(new[] { "B", "D", "_a", "_c" },results.First(r => r.Scope == ProbeScope.All).Members.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("_f0")]
    [InlineData("_f1")]
    [InlineData("_f2")]
    [InlineData("_f3")]
    public void Getter_ConcurrentFirstCall_PublishesOneDelegate(string name)
    {
        var probe = TypeProbe.Of<GetterRaceTarget>();

        Getter<GetterRaceTarget, int>[] results = RunAtOnce(_ => probe.Getter<int>(name));

        Assert.All(results, r => Assert.Same(results[0], r));
    }

    [Theory]
    [InlineData("_f4")]
    [InlineData("_f5")]
    public void Setter_ConcurrentFirstCall_PublishesOneDelegate(string name)
    {
        var probe = TypeProbe.Of<GetterRaceTarget>();

        Setter<GetterRaceTarget, int>[] results = RunAtOnce(_ => probe.Setter<int>(name));

        Assert.All(results, r => Assert.Same(results[0], r));
    }

    [Theory]
    [InlineData("_f6")]
    [InlineData("_f7")]
    public void RefSetter_ConcurrentFirstCall_PublishesOneDelegate(string name)
    {
        var probe = TypeProbe.Of<GetterRaceTarget>();

        RefSetter<GetterRaceTarget, int>[] results = RunAtOnce(_ => probe.RefSetter<int>(name));

        Assert.All(results, r => Assert.Same(results[0], r));
    }

    [Fact]
    public void HitPath_ConcurrentGetSet_EachThreadSeesOwnValues()
    {
        // 생성 이후 히트 경로(이름 · 인덱스 · 강타입)는 상태를 바꾸지 않으므로 락 없이 여러 스레드가 써도 된다.
        var probe = TypeProbe.Of<HitTarget>();
        int valueIndex = probe.GetIndex("_value");
        Getter<HitTarget, string> getLabel = probe.Getter<string>("Label");

        bool[] results = RunAtOnce(i =>
        {
            var target = new HitTarget();
            for (int n = 0; n < 10_000; n++)
            {
                probe[target, "_value"] = n;
                probe.SetValue(target, valueIndex, (int)probe.GetValue(target, valueIndex)! + i);
                probe[target, "Label"] = "t" + i;
                if ((int)probe[target, "_value"]! != n + i || getLabel(target) != "t" + i)
                    return false;
            }
            return true;
        });

        Assert.All(results, Assert.True);
    }
}
