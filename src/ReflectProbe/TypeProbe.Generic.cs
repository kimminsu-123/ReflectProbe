using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using ReflectProbe.Internal;

namespace ReflectProbe
{
    /// <summary>
    /// 강타입 진입점. 이름 기반 API 는 <see cref="TypeProbe"/> 에서 그대로 상속한다.
    /// Getter / Setter / RefSetter 는 멤버별로 1회 생성 후 내부에 캐싱되지만, 반복 호출 경로에서는 반환된 델리게이트를 직접 보관해 쓴다.
    /// </summary>
    public sealed class TypeProbe<T> : TypeProbe
    {
        // 키 = (멤버 이름, 델리게이트 타입). Getter / Setter / RefSetter 는 델리게이트 타입이 달라 한 캐시에 섞이지 않는다.
        private readonly ConcurrentDictionary<(string, Type), Delegate> _accessorCache = new();

        // 매개변수 없는 생성자 팩토리. 처음 쓸 때 1회 판단·생성한다. null = 만들 수 없는 타입.
        private readonly Lazy<Func<T>?> _defaultFactory;

        internal TypeProbe(ProbeScope scope, ProbeBuilder builder)
            : base(typeof(T), scope, builder)
        {
            _defaultFactory = new Lazy<Func<T>?>(BuildDefaultFactory, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// 멤버 1개를 박싱 없이 읽는 델리게이트. TValue 는 멤버 타입과 같거나 참조 변환 · 박싱이 되는 타입이어야 한다.
        /// 없는 이름은 MemberNotFoundException, 읽을 수 없으면 MemberNotReadableException, 서로 다른 값 타입이면 InvalidCastException.
        /// </summary>
        public Getter<T, TValue> Getter<TValue>(string name)
            => GetOrBuildAccessor(name, static (builder, member) => builder.BuildGetter<T, TValue>(member));

        /// <summary>
        /// 멤버 1개를 박싱 없이 쓰는 델리게이트. T 가 값 타입이면 인스턴스 멤버에 쓸 수 없다 (NotSupportedException, RefSetter 를 쓴다).
        /// 없는 이름은 MemberNotFoundException, 쓸 수 없으면 MemberNotWritableException, 서로 다른 값 타입이면 InvalidCastException.
        /// </summary>
        public Setter<T, TValue> Setter<TValue>(string name)
            => GetOrBuildAccessor(name, static (builder, member) => builder.BuildSetter<T, TValue>(member));

        /// <summary>값 타입 대상의 원본을 바꿀 수 있다. 참조 타입에도 쓸 수 있다. 값 타입 제한을 빼면 예외는 Setter 와 같다.</summary>
        public RefSetter<T, TValue> RefSetter<TValue>(string name)
            => GetOrBuildAccessor(name, static (builder, member) => builder.BuildRefSetter<T, TValue>(member));

        /// <summary>매개변수 없는 생성자로 T 를 만든다. 값 타입도 박싱하지 않는다. 만들 수 없으면 MemberNotFoundException.</summary>
        public new T CreateInstance()
            => (_defaultFactory.Value ?? throw new MemberNotFoundException(typeof(T), ".ctor()"))();

        private protected override bool HasDefaultFactory => _defaultFactory.Value != null;

        private protected override object CreateInstanceAsObject() => CreateInstance()!;

        private TDelegate GetOrBuildAccessor<TDelegate>(string name, Func<ProbeBuilder, ProbeMember, TDelegate> build)
            where TDelegate : Delegate
        {
            var key = (name, typeof(TDelegate));
            if (_accessorCache.TryGetValue(key, out Delegate? cached))
                return (TDelegate)cached;

            // 동시에 2번 만들어져도 게시된 1개를 돌려준다 (TryAdd 후 지역 변수를 반환하면 진 스레드가 다른 인스턴스를 받는다 — 13주 수정).
            return (TDelegate)_accessorCache.GetOrAdd(key, build(_builder, Members[name]));
        }

        private Func<T>? BuildDefaultFactory()
        {
            Type type = typeof(T);
            if (type.IsAbstract)
                return null;

            ConstructorInfo? constructor = MethodCollector.FindConstructor(type, Scope, Type.EmptyTypes);
            if (constructor == null && !type.IsValueType)
                return null;

            // struct 는 매개변수 없는 생성자를 선언했으면 그것을, 아니면 initobj 로 default 값을 만든다.
            return _builder.BuildDefaultFactory<T>(constructor);
        }
    }
}
