namespace ReflectProbe
{
    /// <summary>박싱 없는 강타입 getter. static 멤버는 target 을 무시한다.</summary>
    public delegate TValue Getter<in T, out TValue>(T target);

    /// <summary>참조 타입 대상의 강타입 setter.</summary>
    public delegate void Setter<in T, in TValue>(T target, TValue value);

    /// <summary>값 타입(struct) 대상도 원본을 바꿀 수 있는 ref setter.</summary>
    public delegate void RefSetter<T, in TValue>(ref T target, TValue value);

    /// <summary>Activator.CreateInstance(type, args) 대체. 생성자 1개에 고정된다.</summary>
    public delegate object ProbeFactory(object?[] args);

    /// <summary>MethodInfo.Invoke(target, args) 대체. 메서드 1개에 고정된다. void 메서드는 null 을 반환한다.</summary>
    public delegate object? ProbeInvoker(object? target, object?[] args);
}
