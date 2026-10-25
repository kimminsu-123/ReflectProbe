using System;

namespace ReflectProbe
{
    /// <summary>
    /// 강타입 진입점. 이름 기반 API 는 <see cref="TypeProbe"/> 에서 그대로 상속한다.
    /// Getter / Setter / RefSetter 는 멤버별로 1회 생성 후 내부에 캐싱되지만, 반복 호출 경로에서는 반환된 델리게이트를 직접 보관해 쓴다.
    /// </summary>
    public sealed class TypeProbe<T> : TypeProbe
    {
        internal TypeProbe(ProbeScope scope)
            : base(typeof(T), scope)
        {
        }

        public Getter<T, TValue> Getter<TValue>(string name) => throw new NotImplementedException();

        /// <summary>T 가 참조 타입일 때만. 값 타입이면 RefSetter 를 쓴다.</summary>
        public Setter<T, TValue> Setter<TValue>(string name) => throw new NotImplementedException();

        /// <summary>값 타입 대상의 원본을 바꿀 수 있다. 참조 타입에도 쓸 수 있다.</summary>
        public RefSetter<T, TValue> RefSetter<TValue>(string name) => throw new NotImplementedException();

        public new T CreateInstance() => throw new NotImplementedException();
    }
}
