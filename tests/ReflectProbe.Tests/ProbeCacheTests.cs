namespace ReflectProbe.Tests;

public class ProbeCacheTests
{
    [Fact]
    public void Of_GenericAndTypeOverload_ReturnSameInstance()
    {
        Assert.Same(TypeProbe.Of<Player>(), TypeProbe.Of(typeof(Player)));
        Assert.IsType<TypeProbe<Player>>(TypeProbe.Of(typeof(Player)));
    }

    [Fact]
    public void Of_DifferentScope_ReturnsDifferentInstance()
    {
        TypeProbe all = TypeProbe.Of<Player>();
        TypeProbe publicOnly = TypeProbe.Of<Player>(ProbeScope.Public | ProbeScope.Instance);

        Assert.NotSame(all, publicOnly);
        Assert.Equal(ProbeScope.All, all.Scope);
        Assert.Equal(ProbeScope.Public | ProbeScope.Instance, publicOnly.Scope);
        Assert.Same(publicOnly, TypeProbe.Of<Player>(ProbeScope.Public | ProbeScope.Instance));
    }

    [Fact]
    public void Of_ConcurrentFirstCall_ReturnsSingleInstance()
    {
        const int threadCount = 32;
        var results = new TypeProbe[threadCount];
        using var barrier = new Barrier(threadCount);

        Thread[] threads = Enumerable.Range(0, threadCount).Select(i => new Thread(() =>
        {
            barrier.SignalAndWait();
            results[i] = TypeProbe.Of(typeof(ConcurrencyTarget));
        })).ToArray();

        foreach (Thread thread in threads)
            thread.Start();
        foreach (Thread thread in threads)
            thread.Join();

        Assert.All(results, r => Assert.Same(results[0], r));
    }

    [Fact]
    public void Of_InvalidType_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TypeProbe.Of(null!));
        Assert.Throws<ArgumentException>(() => TypeProbe.Of(typeof(List<>)));
        Assert.Throws<ArgumentException>(() => TypeProbe.Of(typeof(int).MakeByRefType()));
        Assert.Throws<ArgumentException>(() => TypeProbe.Of(typeof(int).MakePointerType()));
        Assert.Throws<ArgumentException>(() => TypeProbe.Of(typeof(Span<int>)));
        Assert.Throws<ArgumentException>(() => TypeProbe.Of(typeof(void)));
    }

    [Fact]
    public void Of_ObjectType_HasNoMembers()
    {
        TypeProbe probe = TypeProbe.Of<object>();

        Assert.Empty(probe.Members);
        Assert.Throws<MemberNotFoundException>(() => probe[new object(), "x"]);
    }

    [Fact]
    public void Mode_IsEmitOnCoreClr()
    {
        Assert.Equal(ProbeMode.Emit, TypeProbe.Mode);
    }
}
