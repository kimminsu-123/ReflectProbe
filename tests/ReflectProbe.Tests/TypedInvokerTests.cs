namespace ReflectProbe.Tests;

public class TypedInvokerTests
{
    private static readonly TypeProbe<Enemy> _probe = TypeProbe.Of<Enemy>();

    // ---------- GetInvoker<TDelegate> ----------

    [Fact]
    public void Invoker_InstanceMethod_ReturnsTypedResult()
    {
        var attack = _probe.GetInvoker<Func<Enemy, int, int>>("Attack");

        Assert.Equal(100, attack(new Enemy(100, 50), 2));
    }

    [Fact]
    public void Invoker_DelegateSignatureSelectsOverload()
    {
        var enemy = new Enemy(100, 50);

        // 이름만으로는 Ambiguous 인 Attack 3개를 델리게이트 매개변수로 고른다.
        Assert.Equal(50, _probe.GetInvoker<Func<Enemy, int>>("Attack")(enemy));
        Assert.Equal(100, _probe.GetInvoker<Func<Enemy, int, int>>("Attack")(enemy, 2));
        Assert.Equal(105, _probe.GetInvoker<Func<Enemy, int, int, int>>("Attack")(enemy, 2, 5));
    }

    [Fact]
    public void Invoker_VoidMethod_Action()
    {
        var enemy = new Enemy(100, 50);

        _probe.GetInvoker<Action<Enemy, int>>("TakeDamage")(enemy, 30);

        Assert.Equal(70, enemy.Hp);
    }

    [Fact]
    public void Invoker_PrivateMethod_Invokes()
    {
        Assert.Equal("secret:100", _probe.GetInvoker<Func<Enemy, string>>("Secret")(new Enemy(100, 50)));
    }

    [Fact]
    public void Invoker_StaticMethod_NoTargetParameter()
    {
        Assert.Equal(7, _probe.GetInvoker<Func<int, int, int>>("Add")(3, 4));
    }

    [Fact]
    public void Invoker_TargetAsObject_Castclass()
    {
        var attack = TypeProbe.Of(typeof(Enemy)).GetInvoker<Func<object, int, int>>("Attack");

        Assert.Equal(100, attack(new Enemy(100, 50), 2));
        Assert.Throws<InvalidCastException>(() => attack("not an enemy", 2));
    }

    [Fact]
    public void Invoker_ParentPrivateMethod_DerivedTarget()
    {
        Assert.Equal("creature-secret", TypeProbe.Of<Goblin>().GetInvoker<Func<Goblin, string>>("Secret")(new Goblin()));
    }

    [Fact]
    public void Invoker_VirtualMethod_DispatchesToOverride()
    {
        Assert.Equal("goblin", TypeProbe.Of<Creature>().GetInvoker<Func<Creature, string>>("Speak")(new Goblin()));
    }

    [Fact]
    public void Invoker_InterfaceMethod()
    {
        Assert.Equal(3, TypeProbe.Of<IShape>().GetInvoker<Func<IShape, int>>("Sides")(new Triangle()));
    }

    [Fact]
    public void Invoker_StructByRef_MutatesOriginal()
    {
        var counter = new Counter(1);

        TypeProbe.Of<Counter>().GetInvoker<CounterAction>("Increment")(ref counter);

        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public void Invoker_StructByValue_CallsOnCopy()
    {
        var counter = new Counter(3);
        var probe = TypeProbe.Of<Counter>();

        probe.GetInvoker<Action<Counter>>("Increment")(counter);

        Assert.Equal(3, counter.Count); // 사본만 바뀜
        Assert.Equal("Counter(3)", probe.GetInvoker<Func<Counter, string>>("ToString")(counter));
    }

    [Fact]
    public void Invoker_StaticAndInstanceBothMatch_ThrowsAmbiguous()
    {
        // Twice(int) 인스턴스와 static Twice(Calc, int) 가 모두 Func<Calc, int, int> 에 맞는다.
        Assert.Throws<AmbiguousMemberException>(() => TypeProbe.Of<Calc>().GetInvoker<Func<Calc, int, int>>("Twice"));
    }

    [Fact]
    public void Invoker_ReturnTypeMustMatchExactly()
    {
        var ex = Assert.Throws<MemberNotFoundException>(() => _probe.GetInvoker<Func<Enemy, int, long>>("Attack"));

        Assert.Equal("Attack(Enemy, Int32) : Int64", ex.MemberName);
    }

    [Fact]
    public void Invoker_ParameterTypesMustMatchExactly()
    {
        Assert.Throws<MemberNotFoundException>(() => _probe.GetInvoker<Func<Enemy, long, int>>("Attack"));
        Assert.Throws<MemberNotFoundException>(() => _probe.GetInvoker<Func<Enemy, string, string>>("Describe")); // Describe(object) 만 있음
    }

    [Fact]
    public void Invoker_UnrelatedTargetType_ThrowsMemberNotFound()
    {
        Assert.Throws<MemberNotFoundException>(() => _probe.GetInvoker<Func<string, int, int>>("Attack"));
    }

    [Fact]
    public void Invoker_NonConcreteDelegateType_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => _probe.GetInvoker<Delegate>("Attack"));
    }

    [Fact]
    public void Invoker_SameDelegate_ReturnsCachedInstance()
    {
        Assert.Same(_probe.GetInvoker<Func<Enemy, int, int>>("Attack"), _probe.GetInvoker<Func<Enemy, int, int>>("Attack"));
    }

    [Fact]
    public void Invoker_Call_DoesNotAllocate()
    {
        var enemy = new Enemy(100, 50);
        var attack = _probe.GetInvoker<Func<Enemy, int, int>>("Attack");
        attack(enemy, 2); // JIT

        long before = GC.GetAllocatedBytesForCurrentThread();
        int sum = 0;
        for (int i = 0; i < 10_000; i++)
            sum += attack(enemy, 2);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(1_000_000, sum);
        Assert.Equal(0, allocated);
    }

    // ---------- GetFactory<TDelegate> ----------

    [Fact]
    public void Factory_WithArgs_CreatesInstance()
    {
        Enemy enemy = _probe.GetFactory<Func<int, int, Enemy>>()(100, 50);

        Assert.Equal(100, enemy.Hp);
        Assert.Equal(150, enemy.Attack(3));
    }

    [Fact]
    public void Factory_PrivateConstructor()
    {
        Assert.Equal("hidden", _probe.GetFactory<Func<string, Enemy>>()("hidden").Log);
    }

    [Fact]
    public void Factory_Parameterless()
    {
        Assert.IsType<Enemy>(_probe.GetFactory<Func<Enemy>>()());
    }

    [Fact]
    public void Factory_Struct_DoesNotAllocate()
    {
        var create = TypeProbe.Of<Counter>().GetFactory<Func<int, Counter>>();
        Assert.Equal(7, create(7).Count);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
            create(i);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Factory_ReturnTypeMustBeProbeType()
    {
        var ex = Assert.Throws<MemberNotFoundException>(() => _probe.GetFactory<Func<int, int, object>>());

        Assert.Equal(".ctor(Int32, Int32) : Object", ex.MemberName);
    }

    [Fact]
    public void Factory_ParameterTypesMustMatchExactly()
    {
        Assert.Throws<MemberNotFoundException>(() => _probe.GetFactory<Func<long, int, Enemy>>());
    }

    [Fact]
    public void Factory_AbstractType_ThrowsMemberNotFound()
    {
        Assert.Throws<MemberNotFoundException>(() => TypeProbe.Of<Shape>().GetFactory<Func<Shape>>());
    }

    [Fact]
    public void Factory_SameDelegate_ReturnsCachedInstance()
    {
        Assert.Same(_probe.GetFactory<Func<int, int, Enemy>>(), _probe.GetFactory<Func<int, int, Enemy>>());
    }
}
