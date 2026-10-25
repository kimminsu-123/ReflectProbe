using System;

namespace ReflectProbe
{
    /// <summary>
    /// 타입 1개 × 범위 1개에 대한 접근 진입점. (Type, ProbeScope) 마다 1개만 만들어져 프로세스 종료까지 재사용된다.
    /// 실제 인스턴스는 항상 <see cref="TypeProbe{T}"/> 이므로 Of(typeof(T)) 와 Of&lt;T&gt;() 는 같은 객체를 반환한다.
    /// </summary>
    public abstract class TypeProbe
    {
        private protected TypeProbe(Type type, ProbeScope scope)
        {
            throw new NotImplementedException();
        }

        // ---------- 생성 / 캐시 ----------

        public static TypeProbe Of(Type type, ProbeScope scope = ProbeScope.All) => throw new NotImplementedException();

        public static TypeProbe<T> Of<T>(ProbeScope scope = ProbeScope.All) => throw new NotImplementedException();

        /// <summary>이 프로세스에서 쓰는 접근 코드 생성 방식. 최초 1회 판단 후 고정.</summary>
        public static ProbeMode Mode => throw new NotImplementedException();

        public Type Type { get; }

        public ProbeScope Scope { get; }

        public ProbeMemberSet Members { get; }

        // ---------- 필드 / 프로퍼티 : 이름 기반 ----------

        /// <summary>
        /// 이름으로 읽고 쓴다. static 멤버는 target 을 무시한다.
        /// 없는 이름은 MemberNotFoundException, 방향이 막힌 멤버는 MemberNotReadable/NotWritableException.
        /// </summary>
        public object? this[object? target, string name]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        /// <summary>없거나 읽을 수 없으면 false. 예외를 던지지 않는다.</summary>
        public bool TryGetValue(object? target, string name, out object? value) => throw new NotImplementedException();

        /// <summary>없거나 쓸 수 없으면 false. 예외를 던지지 않는다.</summary>
        public bool TrySetValue(object? target, string name, object? value) => throw new NotImplementedException();

        // ---------- 필드 / 프로퍼티 : 인덱스 기반 (이름 해시 생략) ----------

        public int GetIndex(string name) => throw new NotImplementedException();

        public bool TryGetIndex(string name, out int index) => throw new NotImplementedException();

        public object? GetValue(object? target, int index) => throw new NotImplementedException();

        public void SetValue(object? target, int index, object? value) => throw new NotImplementedException();

        // ---------- 생성자 ----------

        /// <summary>매개변수 없는 생성자가 있거나 값 타입이면 true.</summary>
        public bool CanCreateInstance => throw new NotImplementedException();

        public object CreateInstance() => throw new NotImplementedException();

        /// <summary>매개변수 타입이 정확히 일치하는 생성자의 팩토리. 반환값을 캐싱해서 쓴다.</summary>
        public ProbeFactory GetFactory(params Type[] parameterTypes) => throw new NotImplementedException();

        // ---------- 메서드 ----------

        /// <summary>이름으로 찾는다. 오버로드가 2개 이상이면 AmbiguousMemberException.</summary>
        public ProbeInvoker GetInvoker(string name) => throw new NotImplementedException();

        /// <summary>이름 + 매개변수 타입으로 찾는다. 매개변수 없는 오버로드는 Type.EmptyTypes 를 넘긴다.</summary>
        public ProbeInvoker GetInvoker(string name, params Type[] parameterTypes) => throw new NotImplementedException();

        /// <summary>GetInvoker(name) 결과를 내부에 캐싱해 호출한다.</summary>
        public object? Invoke(object? target, string name, params object?[] args) => throw new NotImplementedException();
    }
}
