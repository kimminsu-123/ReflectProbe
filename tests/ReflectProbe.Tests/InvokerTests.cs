namespace ReflectProbe.Tests;

public class InvokerTests
{
    private static readonly TypeProbe<Enemy> _probe = TypeProbe.Of<Enemy>();

    // ---------- 호출 ----------

    [Fact]
    public void GetInvoker_InstanceMethod_ReturnsBoxedResult()
    {
        ProbeInvoker attack = _probe.GetInvoker("Attack", typeof(int));

        Assert.Equal(100, attack(new Enemy(100, 50), new object?[] { 2 }));
    }

    [Fact]
    public void GetInvoker_VoidMethod_ReturnsNullAndRuns()
    {
        var enemy = new Enemy(100, 50);
        ProbeInvoker takeDamage = _probe.GetInvoker("TakeDamage");

        object? result = takeDamage(enemy, new object?[] { 30 });

        Assert.Null(result);
        Assert.Equal(70, enemy.Hp);
    }

    [Fact]
    public void GetInvoker_PrivateMethod_Invokes()
    {
        Assert.Equal("secret:100", _probe.GetInvoker("Secret")(new Enemy(100, 50), Array.Empty<object?>()));
    }

    [Fact]
    public void GetInvoker_StaticMethod_IgnoresTarget()
    {
        Assert.Equal(7, _probe.GetInvoker("Add")(null, new object?[] { 3, 4 }));
    }

    [Fact]
    public void GetInvoker_NullArgument_PassedToReferenceParameter()
    {
        var enemy = new Enemy();

        _probe.GetInvoker("Rename")(enemy, new object?[] { null });

        Assert.Equal("null", enemy.Log);
    }

    // ---------- 오버로드 규칙 ----------

    [Fact]
    public void GetInvoker_NameOnly_Overloaded_ThrowsAmbiguous()
    {
        var ex = Assert.Throws<AmbiguousMemberException>(() => _probe.GetInvoker("Attack"));

        Assert.Equal("Attack", ex.MemberName);
    }

    [Fact]
    public void GetInvoker_WithParameterTypes_SelectsOverload()
    {
        var enemy = new Enemy(100, 50);

        Assert.Equal(50, _probe.GetInvoker("Attack", Type.EmptyTypes)(enemy, Array.Empty<object?>()));
        Assert.Equal(100, _probe.GetInvoker("Attack", typeof(int))(enemy, new object?[] { 2 }));
        Assert.Equal(105, _probe.GetInvoker("Attack", typeof(int), typeof(int))(enemy, new object?[] { 2, 5 }));
    }

    [Fact]
    public void GetInvoker_ParameterTypesMustMatchExactly()
    {
        // Describe(object) 는 있지만 Describe(string) 은 없다. 대입 가능해도 고르지 않는다.
        var ex = Assert.Throws<MemberNotFoundException>(() => _probe.GetInvoker("Describe", typeof(string)));

        Assert.Equal("Describe(String)", ex.MemberName);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("get_Hp")]   // 프로퍼티 접근자는 메서드로 수집하지 않는다
    [InlineData("Echo")]     // 제네릭 메서드 정의
    [InlineData("TryHit")]   // out 매개변수
    public void GetInvoker_NotCollected_ThrowsMemberNotFound(string name)
    {
        Assert.Throws<MemberNotFoundException>(() => _probe.GetInvoker(name));
    }

    // ---------- 상속 ----------

    [Fact]
    public void GetInvoker_ParentPrivateMethod_Invokes()
    {
        Assert.Equal("creature-secret", TypeProbe.Of<Goblin>().GetInvoker("Secret")(new Goblin(), Array.Empty<object?>()));
    }

    [Fact]
    public void GetInvoker_Override_CollectedOnceAndDispatchesVirtually()
    {
        // override 가 부모 선언과 합쳐져 1개라 이름만으로 찾아도 모호하지 않다.
        Assert.Equal("goblin", TypeProbe.Of<Goblin>().GetInvoker("Speak")(new Goblin(), Array.Empty<object?>()));

        // 부모 타입 probe 로 불러도 가상 호출이라 자식 구현이 실행된다.
        Assert.Equal("goblin", TypeProbe.Of<Creature>().GetInvoker("Speak")(new Goblin(), Array.Empty<object?>()));
    }

    [Fact]
    public void GetInvoker_InterfaceProbe_CallsImplementation()
    {
        // 인터페이스는 DynamicMethod owner 가 될 수 없어 모듈 소속으로 만든다.
        Assert.Equal(3, TypeProbe.Of<IShape>().GetInvoker("Sides")(new Triangle(), Array.Empty<object?>()));
    }

    [Fact]
    public void GetInvoker_NewMethod_ChildWins()
    {
        Assert.Equal("goblin", TypeProbe.Of<Goblin>().GetInvoker("Name")(new Goblin(), Array.Empty<object?>()));
    }

    // ---------- struct 대상 ----------

    [Fact]
    public void Invoke_BoxedStruct_MutatesBox()
    {
        object boxed = new Counter(1);

        TypeProbe.Of<Counter>().Invoke(boxed, "Increment");

        Assert.Equal(2, ((Counter)boxed).Count);
    }

    [Fact]
    public void Invoke_StructOverride_CallsStructImplementation()
    {
        Assert.Equal("Counter(3)", TypeProbe.Of<Counter>().Invoke(new Counter(3), "ToString"));
    }

    // ---------- Invoke (이름 캐시) ----------

    [Fact]
    public void Invoke_ByName_Invokes()
    {
        Assert.Equal(7, _probe.Invoke(null, "Add", 3, 4));
    }

    [Fact]
    public void Invoke_Overloaded_ThrowsAmbiguous()
    {
        Assert.Throws<AmbiguousMemberException>(() => _probe.Invoke(new Enemy(), "Attack", 2));
    }

    [Fact]
    public void ObjectProbe_Invoke_DelegatesToTypeProbe()
    {
        var enemy = new Enemy(100, 50);
        var op = new ObjectProbe(enemy);

        op.Invoke("TakeDamage", 40);

        Assert.Equal(60, enemy.Hp);
    }

    // ---------- 캐시 ----------

    [Fact]
    public void GetInvoker_SameMethod_ReturnsCachedInstance()
    {
        Assert.Same(_probe.GetInvoker("TakeDamage"), _probe.GetInvoker("TakeDamage", typeof(int)));
    }

    [Fact]
    public void GetInvoker_ConcurrentFirstCall_PublishesOneInstance()
    {
        var probe = TypeProbe.Of<Goblin>(ProbeScope.Public | ProbeScope.Instance);
        var results = new ProbeInvoker[32];
        using var start = new ManualResetEventSlim();

        Thread[] threads = Enumerable.Range(0, results.Length).Select(i => new Thread(() =>
        {
            start.Wait();
            results[i] = probe.GetInvoker("Speak");
        })).ToArray();
        foreach (Thread t in threads) t.Start();
        start.Set();
        foreach (Thread t in threads) t.Join();

        Assert.All(results, r => Assert.Same(results[0], r));
    }
}
