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
        private readonly ConcurrentDictionary<(string, Type), object> _getterCache = new();
        private readonly ConcurrentDictionary<(string, Type), object> _setterCache = new();
        private readonly ConcurrentDictionary<(string, Type), object> _refSetterCache = new();

        // 매개변수 없는 생성자 팩토리. 처음 쓸 때 1회 판단·생성한다. null = 만들 수 없는 타입.
        private readonly Lazy<Func<T>?> _defaultFactory;

        internal TypeProbe(ProbeScope scope, ProbeBuilder builder)
            : base(typeof(T), scope, builder)
        {
            _defaultFactory = new Lazy<Func<T>?>(BuildDefaultFactory, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Getter<T, TValue> Getter<TValue>(string name)
        {
            var key = (name, typeof(TValue));
            if (_getterCache.TryGetValue(key, out object? cached))
                return (Getter<T, TValue>)cached;
            if (!Members.TryGet(name, out ProbeMember? member))
                throw new MemberNotFoundException(typeof(T), name);
            Getter<T, TValue> getter = _builder.BuildGetter<T, TValue>(member!);
            _getterCache.TryAdd(key, getter);
            return getter;
        }

        /// <summary>T 가 참조 타입일 때만. 값 타입이면 RefSetter 를 쓴다.</summary>
        public Setter<T, TValue> Setter<TValue>(string name)
        {
            var key = (name, typeof(TValue));
            if (_setterCache.TryGetValue(key, out object? cached))
                return (Setter<T, TValue>)cached;
            if (!Members.TryGet(name, out ProbeMember? member))
                throw new MemberNotFoundException(typeof(T), name);
            Setter<T, TValue> setter = _builder.BuildSetter<T, TValue>(member!);
            _setterCache.TryAdd(key, setter);
            return setter;
        }

        /// <summary>값 타입 대상의 원본을 바꿀 수 있다. 참조 타입에도 쓸 수 있다.</summary>
        public RefSetter<T, TValue> RefSetter<TValue>(string name)
        {
            var key = (name, typeof(TValue));
            if (_refSetterCache.TryGetValue(key, out object? cached))
                return (RefSetter<T, TValue>)cached;
            if (!Members.TryGet(name, out ProbeMember? member))
                throw new MemberNotFoundException(typeof(T), name);
            RefSetter<T, TValue> refSetter = _builder.BuildRefSetter<T, TValue>(member!);
            _refSetterCache.TryAdd(key, refSetter);
            return refSetter;
        }

        /// <summary>매개변수 없는 생성자로 T 를 만든다. 값 타입도 박싱하지 않는다. 만들 수 없으면 MemberNotFoundException.</summary>
        public new T CreateInstance()
            => (_defaultFactory.Value ?? throw new MemberNotFoundException(typeof(T), ".ctor()"))();

        private protected override bool HasDefaultFactory => _defaultFactory.Value != null;

        private protected override object CreateInstanceAsObject() => CreateInstance()!;

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
