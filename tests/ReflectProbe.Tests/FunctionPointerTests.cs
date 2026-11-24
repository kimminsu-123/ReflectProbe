namespace ReflectProbe.Tests;

// 14주 점검 : 함수 포인터도 박싱할 수 없는 타입이라 포인터와 같이 수집하지 않는다 (결정 11 · 23).
// 수정 전 Emit 은 함수 포인터 값을 object 참조로 돌려줘, 그 참조를 쓰는 순간 프로세스가 AccessViolationException 으로 종료됐다.
// 그래서 값을 읽는 테스트 대신 수집 여부만 확인한다.
public unsafe class FunctionPointerTests
{
    [Fact]
    public void FunctionPointerMembers_AreNotCollected()
    {
        TypeProbe probe = TypeProbe.Of<PointerHolder>();

        Assert.True(probe.TryGetIndex("Number", out _));
        Assert.False(probe.TryGetIndex("Field", out _));
        Assert.False(probe.TryGetIndex("Property", out _));
    }

    [Fact]
    public void FunctionPointerSignatures_AreNotCollected()
    {
        TypeProbe probe = TypeProbe.Of<PointerHolder>();

        Assert.Throws<MemberNotFoundException>(() => probe.GetInvoker("Call"));
        Assert.Throws<MemberNotFoundException>(() => probe.GetInvoker("Create"));
        Assert.Equal(7, probe.Invoke(new PointerHolder(), "Plain"));
    }

    [Fact]
    public void FunctionPointerType_IsNotProbeTarget()
    {
        Type pointer = typeof(PointerHolder).GetField("Field")!.FieldType;

        Assert.Throws<ArgumentException>(() => TypeProbe.Of(pointer));
    }

    public class PointerHolder
    {
        public int Number = 7;
        public delegate*<int> Field;
        public delegate*<int> Property { get; set; }

        public int Call(delegate*<int> function) => function();
        public delegate*<int> Create() => null;
        public int Plain() => Number;
    }
}
