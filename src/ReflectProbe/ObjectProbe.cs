using System;

namespace ReflectProbe
{
    /// <summary>
    /// 객체 1개 + TypeProbe 를 묶은 래퍼. struct 라 생성해도 힙 할당이 없다. (FastMember ObjectAccessor 는 호출마다 할당)
    /// default(ObjectProbe) 는 사용할 수 없다.
    /// </summary>
    public readonly struct ObjectProbe
    {
        public ObjectProbe(object target, ProbeScope scope = ProbeScope.All)
        {
            throw new NotImplementedException();
        }

        public object Target { get; }

        public TypeProbe Probe { get; }

        public object? this[string name]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public bool TryGetValue(string name, out object? value) => throw new NotImplementedException();

        public bool TrySetValue(string name, object? value) => throw new NotImplementedException();

        public object? Invoke(string name, params object?[] args) => throw new NotImplementedException();
    }
}
