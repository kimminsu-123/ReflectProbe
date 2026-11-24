namespace ReflectProbe.Tests;

// 14주 (결정 38) : 인터페이스 대상은 상속한 인터페이스의 멤버 · 메서드도 수집한다.
// 파생 인터페이스의 같은 이름이 부모를 가리고(IsHidden), 상속 관계가 없는 부모끼리 같은 이름이면 이름으로는 AmbiguousMemberException.
public class InterfaceTests
{
    private static readonly TypeProbe<IDerived> _probe = TypeProbe.Of<IDerived>();

    // ---------- 프로퍼티 ----------

    [Fact]
    public void InheritedProperty_ReadsAndWrites()
    {
        var target = new Impl();

        _probe[target, "Hp"] = 7;

        Assert.Equal(7, target.Hp);
        Assert.Equal(7, _probe[target, "Hp"]);
        Assert.Equal(2, _probe[target, "Mp"]);
    }

    [Fact]
    public void InheritedProperty_StronglyTyped()
    {
        var target = new Impl();

        _probe.Setter<int>("Hp")(target, 9);

        Assert.Equal(9, _probe.Getter<int>("Hp")(target));
    }

    [Fact]
    public void GenericCollectionInterface_InheritedCount()
    {
        Assert.Equal(2, TypeProbe.Of<IList<int>>()[new List<int> { 1, 2 }, "Count"]);
    }

    [Fact]
    public void Diamond_CollectsSharedParentOnce()
    {
        TypeProbe probe = TypeProbe.Of<IDiamond>();

        Assert.Single(probe.Members, m => m.Name == "Hp");
        Assert.Equal(1, probe[new Impl(), "Hp"]);
    }

    [Fact]
    public void DerivedNewProperty_HidesParent_DepthOffsetReachesParent()
    {
        TypeProbe probe = TypeProbe.Of<IHiding>();
        var target = new HidingImpl();

        Assert.Equal("new", probe[target, "Hp"]);
        Assert.Equal(1, probe[target, "Hp", 1]);
        Assert.True(probe.Members.Single(m => m.Name == "Hp" && m.DeclaringType == typeof(IBase)).IsHidden);
    }

    // IHiding 이 IBase.Hp 를 가렸으므로 IOther 경로로 IBase 를 다시 만나도 모호하지 않다 (C# 규칙과 같음).
    [Fact]
    public void HiddenOnOnePath_IsHiddenOnAllPaths()
    {
        Assert.Equal("new", TypeProbe.Of<IHidingDiamond>()[new HidingImpl(), "Hp"]);
    }

    [Fact]
    public void UnrelatedParents_SameName_IsAmbiguousByName()
    {
        TypeProbe<ITwoParents> probe = TypeProbe.Of<ITwoParents>();
        var target = new TwoParentsImpl();

        Assert.Throws<AmbiguousMemberException>(() => probe[target, "Value"]);
        Assert.Throws<AmbiguousMemberException>(() => probe[target, "Value"] = 1);
        Assert.Throws<AmbiguousMemberException>(() => probe.GetIndex("Value"));
        Assert.Throws<AmbiguousMemberException>(() => probe[target, "Value", 1]);
        Assert.Throws<AmbiguousMemberException>(() => probe.Getter<int>("Value"));
        Assert.Throws<AmbiguousMemberException>(() => probe.Members["Value"]);
        Assert.False(probe.TryGetIndex("Value", out _));
        Assert.False(probe.TryGetValue(target, "Value", out _));
        Assert.False(probe.TrySetValue(target, "Value", 1));
        Assert.False(probe.Members.TryGet("Value", 1, out _));
    }

    [Fact]
    public void UnrelatedParents_SameName_AccessibleByIndex()
    {
        TypeProbe probe = TypeProbe.Of<ITwoParents>();
        var target = new TwoParentsImpl();

        object?[] values = probe.Members.Where(m => m.Name == "Value")
            .OrderBy(m => m.DeclaringType.Name).Select(m => probe.GetValue(target, m.Index)).ToArray();

        Assert.Equal(new object[] { 10, 20 }, values);
        Assert.Equal(5, probe[target, "Own"]);
    }

    // 변성 때문에 IOut<object> 변수에 IOut<string> 을 넣을 수 있지만 상속 관계는 아니다.
    [Fact]
    public void VariantParents_AreNotTreatedAsInheritance()
    {
        Assert.Throws<AmbiguousMemberException>(() => TypeProbe.Of<IVariantParents>().GetIndex("Item1"));
    }

    // ---------- 메서드 ----------

    [Fact]
    public void InheritedMethod_Invokes()
    {
        Assert.Equal(6, _probe.Invoke(new Impl(), "Roll"));
        Assert.Equal(6, _probe.GetInvoker<Func<IDerived, int>>("Roll")(new Impl()));
        Assert.Equal(6, _probe.GetInvoker<Func<IBase, int>>("Roll")(new Impl()));
    }

    [Fact]
    public void DerivedNewMethod_HidesParent()
    {
        Assert.Equal("new roll", TypeProbe.Of<IHiding>().Invoke(new HidingImpl(), "Roll"));
    }

    [Fact]
    public void UnrelatedParents_SameMethod_IsAmbiguous()
    {
        TypeProbe<ITwoParents> probe = TypeProbe.Of<ITwoParents>();

        Assert.Throws<AmbiguousMemberException>(() => probe.GetInvoker("Run"));
        Assert.Throws<AmbiguousMemberException>(() => probe.GetInvoker("Run", Type.EmptyTypes));
        Assert.Throws<AmbiguousMemberException>(() => probe.GetInvoker<Func<ITwoParents, int>>("Run"));
        Assert.Equal(1, probe.GetInvoker<Func<ILeft, int>>("Run")(new TwoParentsImpl()) % 10);
    }

    [Fact]
    public void DefaultImplementation_PrivateParentMember()
    {
        Assert.Equal(8, TypeProbe.Of<IDefaultChild>()[new DefaultImpl(), "Secret"]);
        Assert.Equal(3, TypeProbe.Of<IDefaultChild>().Invoke(new DefaultImpl(), "Default"));
        Assert.Throws<MemberNotFoundException>(() => TypeProbe.Of<IDefaultChild>(ProbeScope.Public | ProbeScope.Instance).GetIndex("Secret"));
    }

    // ---------- static abstract / static virtual : 타입 인수 없이는 부를 수 없어 수집하지 않는다 ----------
    // 수정 전 Emit 은 이 멤버 하나 때문에 타입 전체 접근이 BadImageFormatException 이었다.

    [Fact]
    public void StaticAbstractMembers_AreNotCollected_OthersWork()
    {
        TypeProbe probe = TypeProbe.Of(typeof(IStaticAbstract));

        Assert.Equal(5, probe[new StaticAbstractImpl(), "Hp"]);
        probe[null, "Shared"] = 4;
        Assert.Equal(4, probe[null, "Shared"]);
        Assert.False(probe.TryGetIndex("Abstract", out _));
        Assert.False(probe.TryGetIndex("Virtual", out _));
        Assert.Throws<MemberNotFoundException>(() => probe.GetInvoker("Make"));
        Assert.Equal(3, probe.Invoke(null, "Plain", 3));
        Assert.Equal(30, TypeProbe.Of<StaticAbstractImpl>()[null, "Abstract"]);     // 구현 클래스 쪽은 일반 static 멤버
    }

    // ---------- 픽스처 ----------

    public interface IBase { int Hp { get; set; } int Roll(); }
    public interface IDerived : IBase { int Mp { get; set; } }
    public interface IOther : IBase { }
    public interface IDiamond : IDerived, IOther { }

    public class Impl : IDiamond
    {
        public int Hp { get; set; } = 1;
        public int Mp { get; set; } = 2;
        public int Roll() => 6;
    }

    public interface IHiding : IBase { new string Hp { get; } new string Roll(); }
    public interface IHidingDiamond : IHiding, IOther { }

    public class HidingImpl : IHidingDiamond
    {
        string IHiding.Hp => "new";
        string IHiding.Roll() => "new roll";
        int IBase.Hp { get; set; } = 1;
        int IBase.Roll() => 6;
    }

    public interface ILeft { int Value { get; set; } int Run(); }
    public interface IRight { int Value { get; set; } int Run(); }
    public interface ITwoParents : ILeft, IRight { int Own { get; } }

    public class TwoParentsImpl : ITwoParents
    {
        int ILeft.Value { get; set; } = 10;
        int IRight.Value { get; set; } = 20;
        int ILeft.Run() => 1;
        int IRight.Run() => 2;
        public int Own => 5;
    }

    public interface IOut<out T> { T Item1 { get; } }
    public interface IVariantParents : IOut<string>, IOut<object> { }

    public interface IDefault { private int Secret => 8; int Default() => Secret - 5; }
    public interface IDefaultChild : IDefault { }
    public class DefaultImpl : IDefaultChild { }

    public interface IStaticAbstract
    {
        static abstract int Abstract { get; }
        static virtual int Virtual => 6;
        static int Shared { get; set; }
        static abstract int Make(int a);
        static int Plain(int a) => a;
        int Hp { get; }
    }

    public class StaticAbstractImpl : IStaticAbstract
    {
        public static int Abstract => 30;
        public static int Make(int a) => a;
        public int Hp => 5;
    }
}
