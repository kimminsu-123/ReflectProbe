namespace ReflectProbe.Tests;

// 14주 버그 수정 (결정 35) : 강타입 API 의 TValue 가 멤버 타입과 다른 값 타입이면 델리게이트를 만들 때 막는다.
// 수정 전 Emit 은 변환 IL 없이 비트를 그대로 재해석해 예외 없이 틀린 값을 냈고 (int -7 → float NaN), 폴백은 호출할 때 InvalidCastException 을 냈다.
public class TypedMismatchTests
{
    private static readonly TypeProbe<Player> _probe = TypeProbe.Of<Player>();
    private static readonly TypeProbe<MismatchTarget> _target = TypeProbe.Of<MismatchTarget>();
    private static readonly TypeProbe<MutablePoint> _pointProbe = TypeProbe.Of<MutablePoint>();

    // ---------- 값 타입 ↔ 값 타입 : 만들 때 InvalidCastException ----------

    [Fact]
    public void Getter_DifferentValueType_ThrowsWhenBuilt()
    {
        Assert.Throws<InvalidCastException>(() => _probe.Getter<float>("_hp"));
        Assert.Throws<InvalidCastException>(() => _probe.Getter<long>("_hp"));
        Assert.Throws<InvalidCastException>(() => _probe.Getter<short>("Level"));
        Assert.Throws<InvalidCastException>(() => _probe.Getter<MutablePoint>("_hp"));
    }

    [Fact]
    public void Setter_DifferentValueType_ThrowsWhenBuilt()
    {
        Assert.Throws<InvalidCastException>(() => _probe.Setter<float>("_hp"));
        Assert.Throws<InvalidCastException>(() => _probe.Setter<long>("Level"));
    }

    [Fact]
    public void RefSetter_DifferentValueType_ThrowsWhenBuilt()
    {
        Assert.Throws<InvalidCastException>(() => _probe.RefSetter<long>("_hp"));
        Assert.Throws<InvalidCastException>(() => _pointProbe.RefSetter<float>("X"));
    }

    [Fact]
    public void EnumAndNullable_AreDifferentValueTypes()
    {
        Assert.Throws<InvalidCastException>(() => _target.Getter<int>("Day"));
        Assert.Throws<InvalidCastException>(() => _target.Getter<int>("Maybe"));
        Assert.Throws<InvalidCastException>(() => _target.Setter<int>("Maybe"));
    }

    [Fact]
    public void Mismatch_MessageNamesMemberAndTypes()
    {
        var e = Assert.Throws<InvalidCastException>(() => _probe.Getter<float>("_hp"));

        Assert.Contains("_hp", e.Message);
        Assert.Contains("System.Int32", e.Message);
        Assert.Contains("System.Single", e.Message);
    }

    // ---------- 정확 일치 · 참조 변환 · 박싱은 그대로 ----------

    [Fact]
    public void ExactEnumAndNullable_Work()
    {
        var target = new MismatchTarget();

        _target.Setter<DayOfWeek>("Day")(target, DayOfWeek.Friday);
        _target.Setter<int?>("Maybe")(target, 5);

        Assert.Equal(DayOfWeek.Friday, _target.Getter<DayOfWeek>("Day")(target));
        Assert.Equal(5, _target.Getter<int?>("Maybe")(target));
    }

    [Fact]
    public void ReferenceConversionAndBoxing_Work()
    {
        var target = new MismatchTarget { Text = "abc", Anything = 7 };

        Assert.Equal("abc", _target.Getter<object>("Text")(target));
        Assert.Equal(7, _target.Getter<int>("Anything")(target));      // object → unbox
        Assert.Equal("abc", _target.Getter<IComparable>("Text")(target));

        _target.Setter<object>("Text")(target, "def");                 // castclass
        Assert.Equal("def", target.Text);
        Assert.Throws<InvalidCastException>(() => _target.Setter<object>("Text")(target, 1));
    }

    // ---------- struct 대상 Setter ----------

    [Fact]
    public void Setter_StructInstanceMember_ThrowsNotSupported()
    {
        var e = Assert.Throws<NotSupportedException>(() => _pointProbe.Setter<int>("X"));
        Assert.Contains("RefSetter", e.Message);
        Assert.Throws<NotSupportedException>(() => _pointProbe.Setter<int>("Y"));
    }

    [Fact]
    public void Setter_StructStaticMember_Works()
    {
        TypeProbe.Of<StructWithStatic>().Setter<int>("Total")(default, 12);

        Assert.Equal(12, StructWithStatic.Total);
    }

    // ---------- 우선순위 : 읽기 · 쓰기 불가가 타입 불일치보다 먼저 ----------

    [Fact]
    public void NotReadableAndNotWritable_TakePrecedence()
    {
        Assert.Throws<MemberNotReadableException>(() => _probe.Getter<float>("SecretValue"));
        Assert.Throws<MemberNotWritableException>(() => _probe.Setter<float>("Score"));
        Assert.Throws<MemberNotWritableException>(() => _probe.RefSetter<float>("Score"));
    }

#pragma warning disable CS0649 // 리플렉션으로만 쓰는 필드
    private class MismatchTarget
    {
        public DayOfWeek Day;
        public int? Maybe;
        public string Text = "";
        public object? Anything;
    }

    private struct StructWithStatic
    {
        public static int Total;
    }
#pragma warning restore CS0649
}
