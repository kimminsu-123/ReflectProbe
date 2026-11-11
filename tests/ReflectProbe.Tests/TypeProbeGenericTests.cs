namespace ReflectProbe.Tests;

public class TypeProbeGenericTests
{
    private static readonly TypeProbe<Player> _probe = TypeProbe.Of<Player>();
    private static readonly TypeProbe<MutablePoint> _pointProbe = TypeProbe.Of<MutablePoint>();

    // ---------- Getter ----------

    [Fact]
    public void Getter_PrivateField_ReadsCorrectly()
    {
        var player = new Player();
        var getter = _probe.Getter<int>("_hp");

        Assert.Equal(50, getter(player));
    }

    [Fact]
    public void Getter_PublicProperty_ReadsCorrectly()
    {
        var player = new Player();
        var getter = _probe.Getter<int>("Level");

        Assert.Equal(1, getter(player));
    }

    [Fact]
    public void Getter_BoxedReturnType_ReturnsBoxedValue()
    {
        var player = new Player();
        var getter = _probe.Getter<object>("_hp");

        Assert.Equal(50, getter(player));
    }

    [Fact]
    public void Getter_SetOnlyProperty_ThrowsMemberNotReadable()
    {
        Assert.Throws<MemberNotReadableException>(() => _probe.Getter<int>("SecretValue"));
    }

    [Fact]
    public void Getter_UnknownMember_ThrowsMemberNotFound()
    {
        Assert.Throws<MemberNotFoundException>(() => _probe.Getter<int>("nope"));
    }

    [Fact]
    public void Getter_Caches_SameInstanceOnRepeatCall()
    {
        var g1 = _probe.Getter<int>("_hp");
        var g2 = _probe.Getter<int>("_hp");

        Assert.Same(g1, g2);
    }

    // ---------- Setter ----------

    [Fact]
    public void Setter_PrivateField_WritesCorrectly()
    {
        var player = new Player();
        var setter = _probe.Setter<int>("_hp");

        setter(player, 30);
        Assert.Equal(30, player.Hp);
    }

    [Fact]
    public void Setter_PublicProperty_WritesCorrectly()
    {
        var player = new Player();
        var setter = _probe.Setter<int>("Level");

        setter(player, 5);
        Assert.Equal(5, player.Level);
    }

    [Fact]
    public void Setter_BoxedValueType_WritesCorrectly()
    {
        var player = new Player();
        var setter = _probe.Setter<object>("_hp");

        setter(player, 77);
        Assert.Equal(77, player.Hp);
    }

    [Fact]
    public void Setter_GetOnlyProperty_ThrowsMemberNotWritable()
    {
        Assert.Throws<MemberNotWritableException>(() => _probe.Setter<int>("Score"));
    }

    [Fact]
    public void Setter_UnknownMember_ThrowsMemberNotFound()
    {
        Assert.Throws<MemberNotFoundException>(() => _probe.Setter<int>("nope"));
    }

    [Fact]
    public void Setter_Caches_SameInstanceOnRepeatCall()
    {
        var s1 = _probe.Setter<int>("_hp");
        var s2 = _probe.Setter<int>("_hp");

        Assert.Same(s1, s2);
    }

    // ---------- RefSetter ----------

    [Fact]
    public void RefSetter_StructPublicField_ModifiesOriginal()
    {
        var refSetter = _pointProbe.RefSetter<int>("X");
        var point = new MutablePoint();

        refSetter(ref point, 42);
        Assert.Equal(42, point.X);
    }

    [Fact]
    public void RefSetter_StructPrivateField_ModifiesOriginal()
    {
        var refSetter = _pointProbe.RefSetter<int>("_y");
        var point = new MutablePoint();

        refSetter(ref point, 99);
        Assert.Equal(99, point.Y);
    }

    [Fact]
    public void RefSetter_ClassField_WritesCorrectly()
    {
        var refSetter = _probe.RefSetter<int>("_hp");
        var player = new Player();

        refSetter(ref player, 10);
        Assert.Equal(10, player.Hp);
    }

    [Fact]
    public void RefSetter_Caches_SameInstanceOnRepeatCall()
    {
        var r1 = _pointProbe.RefSetter<int>("X");
        var r2 = _pointProbe.RefSetter<int>("X");

        Assert.Same(r1, r2);
    }

    // ---------- 상속 ----------

    [Fact]
    public void Getter_InheritedPrivateField_ReadsCorrectly()
    {
        var dogProbe = TypeProbe.Of<Dog>();
        var dog = new Dog();
        dogProbe[dog, "_age"] = 7;

        var getter = dogProbe.Getter<int>("_age");
        Assert.Equal(7, getter(dog));
    }

    [Fact]
    public void Setter_OverriddenProperty_CallsOverride()
    {
        var dogProbe = TypeProbe.Of<Dog>();
        var dog = new Dog();
        var getter = dogProbe.Getter<string>("Sound");

        Assert.Equal("woof", getter(dog));
    }
}
