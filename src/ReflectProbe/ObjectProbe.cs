using System;

namespace ReflectProbe
{
    /// <summary>
    /// 객체 1개 + TypeProbe 를 묶은 래퍼. struct 라 생성해도 힙 할당이 없다. (FastMember ObjectAccessor 는 호출마다 할당)
    /// default(ObjectProbe) 는 사용할 수 없다.
    /// </summary>
    public readonly struct ObjectProbe
    {
        /// <summary>target 의 실행 시점 타입으로 TypeProbe 를 찾는다. target 이 null 이면 ArgumentNullException.</summary>
        public ObjectProbe(object target, ProbeScope scope = ProbeScope.All)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            Target = target;
            Probe = TypeProbe.Of(target.GetType(), scope);
        }

        /// <summary>감싼 객체. 값 타입은 박싱된 사본이라 원본 변수와 별개다.</summary>
        public object Target { get; }

        /// <summary>Target 타입의 TypeProbe. 인덱스 접근 · 강타입 API 는 여기서 꺼내 쓴다.</summary>
        public TypeProbe Probe { get; }

        /// <summary>probe.Probe[Target, name] 과 같다. 예외도 같다.</summary>
        public object? this[string name]
        {
            get => Probe[Target, name];
            set => Probe[Target, name] = value;
        }

        /// <summary>없거나 읽을 수 없으면 false.</summary>
        public bool TryGetValue(string name, out object? value) => Probe.TryGetValue(Target, name, out value);

        /// <summary>없거나 쓸 수 없으면 false.</summary>
        public bool TrySetValue(string name, object? value) => Probe.TrySetValue(Target, name, value);

        /// <summary>이름으로 Target 의 메서드를 호출한다. 오버로드가 2개 이상이면 AmbiguousMemberException.</summary>
        public object? Invoke(string name, params object?[] args) => Probe.Invoke(Target, name, args);
    }
}
