namespace ReflectProbe.Tests;

public class ConstructorTests
{
    // ---------- CreateInstance (매개변수 없음) ----------

    [Fact]
    public void CreateInstance_Class_UsesParameterlessConstructor()
    {
        Enemy enemy = TypeProbe.Of<Enemy>().CreateInstance();

        Assert.NotNull(enemy);
        Assert.Equal(0, enemy.Hp);
    }

    [Fact]
    public void CreateInstance_NonGeneric_ReturnsObject()
    {
        object enemy = TypeProbe.Of(typeof(Enemy)).CreateInstance();

        Assert.IsType<Enemy>(enemy);
    }

    [Fact]
    public void CreateInstance_Struct_ReturnsDefault()
    {
        MutablePoint point = TypeProbe.Of<MutablePoint>().CreateInstance();

        Assert.Equal(0, point.X);
    }

    [Fact]
    public void CreateInstance_StructWithParameterlessConstructor_CallsIt()
    {
        Assert.Equal(42, TypeProbe.Of<DefaultedStruct>().CreateInstance().Value);
        Assert.Equal(42, ((DefaultedStruct)TypeProbe.Of(typeof(DefaultedStruct)).CreateInstance()).Value);
    }

    [Fact]
    public void CreateInstance_PrivateConstructor_AllScope_Creates()
    {
        var probe = TypeProbe.Of<Singleton>();

        Assert.True(probe.CanCreateInstance);
        Assert.IsType<Singleton>(probe.CreateInstance());
    }

    [Fact]
    public void CreateInstance_PrivateConstructor_PublicScope_Throws()
    {
        var probe = TypeProbe.Of<Singleton>(ProbeScope.Public | ProbeScope.Instance);

        Assert.False(probe.CanCreateInstance);
        Assert.Throws<MemberNotFoundException>(() => probe.CreateInstance());
    }

    [Theory]
    [InlineData(typeof(Shape))]       // 추상 클래스 (public 생성자가 있어도)
    [InlineData(typeof(IShape))]      // 인터페이스
    [InlineData(typeof(GameConfig))]  // static 클래스
    [InlineData(typeof(string))]      // 매개변수 없는 생성자 없음
    public void CreateInstance_NotCreatable_CanCreateFalseAndThrows(Type type)
    {
        var probe = TypeProbe.Of(type);

        Assert.False(probe.CanCreateInstance);
        Assert.Throws<MemberNotFoundException>(() => probe.CreateInstance());
    }

    [Fact]
    public void CreateInstance_StructGeneric_DoesNotAllocate()
    {
        var probe = TypeProbe.Of<MutablePoint>();
        probe.CreateInstance(); // 팩토리 생성 · JIT

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10_000; i++)
            probe.CreateInstance();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    // ---------- GetFactory (매개변수 있음) ----------

    [Fact]
    public void GetFactory_WithArgs_CreatesInstance()
    {
        ProbeFactory factory = TypeProbe.Of<Enemy>().GetFactory(typeof(int), typeof(int));

        var enemy = (Enemy)factory(new object?[] { 100, 50 });

        Assert.Equal(100, enemy.Hp);
        Assert.Equal(150, enemy.Attack(3));
    }

    [Fact]
    public void GetFactory_PrivateConstructor_Creates()
    {
        ProbeFactory factory = TypeProbe.Of<Enemy>().GetFactory(typeof(string));

        Assert.Equal("hidden", ((Enemy)factory(new object?[] { "hidden" })).Log);
    }

    [Fact]
    public void GetFactory_EmptyTypes_ParameterlessConstructor()
    {
        ProbeFactory factory = TypeProbe.Of<Enemy>().GetFactory(Type.EmptyTypes);

        Assert.IsType<Enemy>(factory(Array.Empty<object?>()));
    }

    [Fact]
    public void GetFactory_StructConstructor_ReturnsBoxedStruct()
    {
        ProbeFactory factory = TypeProbe.Of<Counter>().GetFactory(typeof(int));

        Assert.Equal(7, ((Counter)factory(new object?[] { 7 })).Count);
    }

    [Fact]
    public void GetFactory_TypesMustMatchExactly()
    {
        var probe = TypeProbe.Of<Enemy>();

        // (long, int) 생성자는 없다. 기본 Binder 처럼 변환 가능한 생성자를 고르지 않는다.
        var ex = Assert.Throws<MemberNotFoundException>(() => probe.GetFactory(typeof(long), typeof(int)));
        Assert.Equal(".ctor(Int64, Int32)", ex.MemberName);
    }

    [Fact]
    public void GetFactory_AbstractType_Throws()
    {
        Assert.Throws<MemberNotFoundException>(() => TypeProbe.Of<Shape>().GetFactory(Type.EmptyTypes));
    }

    [Fact]
    public void GetFactory_SameConstructor_ReturnsCachedInstance()
    {
        var probe = TypeProbe.Of<Enemy>();

        Assert.Same(probe.GetFactory(typeof(int), typeof(int)), probe.GetFactory(typeof(int), typeof(int)));
    }

    [Fact]
    public void GetFactory_WrongArgumentType_InvalidCast()
    {
        ProbeFactory factory = TypeProbe.Of<Enemy>().GetFactory(typeof(int), typeof(int));

        Assert.Throws<InvalidCastException>(() => factory(new object?[] { 100L, 50 }));
    }

    [Fact]
    public void GetFactory_TooFewArguments_IndexOutOfRange()
    {
        ProbeFactory factory = TypeProbe.Of<Enemy>().GetFactory(typeof(int), typeof(int));

        Assert.Throws<IndexOutOfRangeException>(() => factory(new object?[] { 100 }));
    }
}
