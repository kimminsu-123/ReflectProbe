using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ReflectProbe.Internal;

namespace ReflectProbe
{
    /// <summary>
    /// 타입 1개 × 범위 1개에 대한 접근 진입점. (Type, ProbeScope) 마다 1개만 만들어져 프로세스 종료까지 재사용된다.
    /// 실제 인스턴스는 항상 <see cref="TypeProbe{T}"/> 이므로 Of(typeof(T)) 와 Of&lt;T&gt;() 는 같은 객체를 반환한다.
    /// </summary>
    public abstract class TypeProbe
    {
        // (index, target) → value / (index, target, value). 타입 전체 멤버를 IL switch 로 분기하는 델리게이트 1개씩.
        private readonly Func<int, object?, object?> _getter;
        private readonly Action<int, object?, object?> _setter;

        // builder 는 테스트에서 ReflectionProbeBuilder 를 직접 넣을 수 있도록 생성자로 받는다.
        private protected ProbeBuilder _builder;

        // 메서드 목록은 Invoker 를 처음 찾을 때 1회 수집한다. (필드 접근만 쓰는 타입은 비용 없음)
        private readonly Lazy<Dictionary<string, MethodInfo[]>> _methods;

        // 생성자 · 메서드당 델리게이트 1개. 동시에 2번 만들어져도 게시는 1개.
        private readonly ConcurrentDictionary<ConstructorInfo, ProbeFactory> _factoryCache = new();
        private readonly ConcurrentDictionary<MethodInfo, ProbeInvoker> _invokerCache = new();
        private readonly ConcurrentDictionary<string, ProbeInvoker> _invokerByName = new(StringComparer.Ordinal);

        // 강타입 Factory · Invoker. 같은 생성자 · 메서드라도 델리게이트 타입마다 IL 이 다르다.
        private readonly ConcurrentDictionary<(MemberInfo, Type), Delegate> _typedCache = new();

        private protected TypeProbe(Type type, ProbeScope scope, ProbeBuilder builder)
        {
            Type = type;
            Scope = scope;
            _builder = builder;

            ProbeMember[] members = MemberCollector.Collect(type, scope);
            Members = new ProbeMemberSet(type, members);
            _getter = builder.BuildGetter(type, members);
            _setter = builder.BuildSetter(type, members);
            _methods = new Lazy<Dictionary<string, MethodInfo[]>>(
                () => MethodCollector.Collect(type, scope), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        // ---------- 생성 / 캐시 ----------

        public static TypeProbe Of(Type type, ProbeScope scope = ProbeScope.All) => ProbeCache.GetOrCreate(type, scope);

        public static TypeProbe<T> Of<T>(ProbeScope scope = ProbeScope.All) => (TypeProbe<T>)ProbeCache.GetOrCreate(typeof(T), scope);

        /// <summary>이 프로세스에서 쓰는 접근 코드 생성 방식. 최초 1회 판단 후 고정.</summary>
        public static ProbeMode Mode => ProbeBuilder.Current.Mode;

        public Type Type { get; }

        public ProbeScope Scope { get; }

        public ProbeMemberSet Members { get; }

        // ---------- 필드 / 프로퍼티 : 이름 기반 ----------

        /// <summary>
        /// 이름으로 읽고 쓴다. static 멤버는 target 을 무시한다.
        /// 없는 이름은 MemberNotFoundException, 방향이 막힌 멤버는 MemberNotReadable/NotWritableException.
        /// 인스턴스 멤버에 null target 은 NullReferenceException, 타입이 맞지 않는 target · value 는 InvalidCastException 이
        /// 그대로 전파된다. (히트 경로에 검사 비용을 두지 않는다)
        /// 값 타입 target 은 박스 안의 값을 직접 수정한다.
        /// </summary>
        public object? this[object? target, string name]
        {
            get => _getter(GetIndex(name), target);
            set => _setter(GetIndex(name), target, value);
        }

        /// <summary>
        /// 같은 이름의 부모 멤버를 선언 깊이로 지정해 읽고 쓴다. probe[p, "_tag", 1] → 직속 부모가 선언한 _tag.
        /// 0 은 this[target, name] 과 같다. IL switch 는 그대로 쓰고 인덱스 탐색만 다르다.
        /// </summary>
        public object? this[object? target, string name, int depthOffset]
        {
            get => _getter(GetIndex(name, depthOffset), target);
            set => _setter(GetIndex(name, depthOffset), target, value);
        }

        /// <summary>없거나 읽을 수 없으면 false. 예외를 던지지 않는다.</summary>
        public bool TryGetValue(object? target, string name, out object? value)
        {
            if (Members.TryGet(name, out ProbeMember? member) && member.CanRead)
            {
                value = _getter(member.Index, target);
                return true;
            }

            value = null;
            return false;
        }

        /// <summary>없거나 쓸 수 없으면 false. 예외를 던지지 않는다.</summary>
        public bool TrySetValue(object? target, string name, object? value)
        {
            if (Members.TryGet(name, out ProbeMember? member) && member.CanWrite)
            {
                _setter(member.Index, target, value);
                return true;
            }

            return false;
        }

        // ---------- 필드 / 프로퍼티 : 인덱스 기반 (이름 해시 생략) ----------

        public int GetIndex(string name) => Members.TryGetIndex(name, out int index) ? index : throw new MemberNotFoundException(Type, name);

        /// <summary>이름 + 선언 깊이의 인덱스. 반복 호출할 때는 받아 두고 GetValue / SetValue 로 쓴다.</summary>
        public int GetIndex(string name, int depthOffset)
            => Members.TryGet(name, depthOffset, out ProbeMember? member) ? member.Index : throw new MemberNotFoundException(Type, name);

        public bool TryGetIndex(string name, out int index) => Members.TryGetIndex(name, out index);

        /// <summary>범위 밖 index 는 ArgumentOutOfRangeException. IsHidden 멤버도 접근할 수 있다.</summary>
        public object? GetValue(object? target, int index) => _getter(index, target);

        /// <summary>범위 밖 index 는 ArgumentOutOfRangeException. IsHidden 멤버도 접근할 수 있다.</summary>
        public void SetValue(object? target, int index, object? value) => _setter(index, target, value);

        // ---------- 생성자 ----------

        /// <summary>범위 안에 매개변수 없는 생성자가 있거나 값 타입이면 true. 추상 타입 · 인터페이스는 false.</summary>
        public bool CanCreateInstance => HasDefaultFactory;

        /// <summary>
        /// 매개변수 없는 생성자로 만든다. 값 타입은 생성자가 없으면 default 값을 박싱해 돌려준다.
        /// 만들 수 없으면 MemberNotFoundException. 박싱 없이 받으려면 TypeProbe&lt;T&gt;.CreateInstance() 를 쓴다.
        /// </summary>
        public object CreateInstance() => CreateInstanceAsObject();

        /// <summary>
        /// 매개변수 타입이 정확히 일치하는 생성자의 팩토리. 생성자당 1회 생성 후 캐싱하지만, 찾는 비용이 있으므로 반환값을 보관해 쓴다.
        /// args 개수가 틀리면 IndexOutOfRangeException, 타입이 틀리면 InvalidCastException 이 그대로 전파된다.
        /// </summary>
        public ProbeFactory GetFactory(params Type[] parameterTypes)
        {
            if (parameterTypes is null)
                throw new ArgumentNullException(nameof(parameterTypes));

            ConstructorInfo constructor = MethodCollector.FindConstructor(Type, Scope, parameterTypes)
                ?? throw new MemberNotFoundException(Type, MethodCollector.FormatSignature(".ctor", parameterTypes));

            if (_factoryCache.TryGetValue(constructor, out ProbeFactory? cached))
                return cached;
            return _factoryCache.GetOrAdd(constructor, _builder.BuildFactory(constructor));
        }

        private protected abstract bool HasDefaultFactory { get; }

        private protected abstract object CreateInstanceAsObject();

        // ---------- 메서드 ----------

        /// <summary>이름으로 찾는다. 오버로드가 2개 이상이면 AmbiguousMemberException.</summary>
        public ProbeInvoker GetInvoker(string name)
        {
            MethodInfo[] overloads = FindOverloads(name);
            if (overloads.Length > 1)
                throw new AmbiguousMemberException(Type, name);
            return GetOrBuildInvoker(overloads[0]);
        }

        /// <summary>이름 + 매개변수 타입으로 찾는다. 타입은 정확히 일치해야 한다. 매개변수 없는 오버로드는 Type.EmptyTypes 를 넘긴다.</summary>
        public ProbeInvoker GetInvoker(string name, params Type[] parameterTypes)
        {
            if (parameterTypes is null)
                throw new ArgumentNullException(nameof(parameterTypes));

            foreach (MethodInfo method in FindOverloads(name))
            {
                if (MethodCollector.ParametersEqual(method.GetParameters(), parameterTypes))
                    return GetOrBuildInvoker(method);
            }

            throw new MemberNotFoundException(Type, MethodCollector.FormatSignature(name, parameterTypes));
        }

        /// <summary>
        /// GetInvoker(name) 결과를 이름으로 캐싱해 호출한다. static 메서드는 target 을 무시한다.
        /// 값 타입 target 은 박스 안의 값으로 호출하므로, 값을 바꾸는 메서드는 박스 원본을 수정한다.
        /// </summary>
        public object? Invoke(object? target, string name, params object?[] args)
        {
            if (!_invokerByName.TryGetValue(name, out ProbeInvoker? invoker))
                invoker = _invokerByName.GetOrAdd(name, GetInvoker(name));
            return invoker(target, args);
        }

        // ---------- 강타입 생성자 · 메서드 (결정 27) ----------

        /// <summary>
        /// 델리게이트 시그니처 그대로 생성자를 호출한다. 매개변수 · 반환 타입은 정확히 일치해야 한다.
        /// <code>var create = probe.GetFactory&lt;Func&lt;int, int, Enemy&gt;&gt;(); create(100, 50);</code>
        /// 인자 배열 · 박싱이 없어 할당은 만들어지는 객체뿐이다. 맞는 생성자가 없으면 MemberNotFoundException.
        /// </summary>
        public TDelegate GetFactory<TDelegate>() where TDelegate : Delegate
        {
            Type delegateType = typeof(TDelegate);
            Type returnType = DelegateSignature.InvokeMethod(delegateType).ReturnType;
            Type[] parameterTypes = DelegateSignature.ParameterTypes(delegateType);

            ConstructorInfo? constructor = returnType == Type ? MethodCollector.FindConstructor(Type, Scope, parameterTypes) : null;
            if (constructor == null)
                throw new MemberNotFoundException(Type, MethodCollector.FormatSignature(".ctor", parameterTypes, returnType));
            return (TDelegate)GetOrBuildTyped(constructor, delegateType);
        }

        /// <summary>
        /// 델리게이트 시그니처 그대로 메서드를 호출한다. 인스턴스 메서드는 첫 매개변수가 대상이다.
        /// <code>var attack = probe.GetInvoker&lt;Func&lt;Enemy, int, int&gt;&gt;("Attack"); attack(enemy, 2);</code>
        /// 매개변수 타입이 오버로드를 정하므로 이름이 같은 메서드가 여러 개여도 된다. 매개변수 · 반환 타입은 정확히 일치,
        /// 대상은 class 면 부모 타입 · object 도 허용(castclass), struct 면 ref T(원본 수정) 또는 T(사본).
        /// 맞는 메서드가 없으면 MemberNotFoundException, static 과 인스턴스가 모두 맞으면 AmbiguousMemberException.
        /// </summary>
        public TDelegate GetInvoker<TDelegate>(string name) where TDelegate : Delegate
        {
            Type delegateType = typeof(TDelegate);
            Type returnType = DelegateSignature.InvokeMethod(delegateType).ReturnType;
            Type[] parameterTypes = DelegateSignature.ParameterTypes(delegateType);

            MethodInfo? match = null;
            foreach (MethodInfo method in FindOverloads(name))
            {
                if (!MethodCollector.MatchesInvoker(method, parameterTypes, returnType))
                    continue;
                if (match != null)
                    throw new AmbiguousMemberException(Type, name);
                match = method;
            }

            if (match == null)
                throw new MemberNotFoundException(Type, MethodCollector.FormatSignature(name, parameterTypes, returnType));
            return (TDelegate)GetOrBuildTyped(match, delegateType);
        }

        private Delegate GetOrBuildTyped(MemberInfo member, Type delegateType)
        {
            var key = (member, delegateType);
            if (_typedCache.TryGetValue(key, out Delegate? cached))
                return cached;

            Delegate built = member is ConstructorInfo constructor
                ? _builder.BuildTypedFactory(constructor, delegateType)
                : _builder.BuildTypedInvoker((MethodInfo)member, delegateType);
            return _typedCache.GetOrAdd(key, built);
        }

        private MethodInfo[] FindOverloads(string name)
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));
            return _methods.Value.TryGetValue(name, out MethodInfo[]? overloads) ? overloads : throw new MemberNotFoundException(Type, name);
        }

        private ProbeInvoker GetOrBuildInvoker(MethodInfo method)
        {
            if (_invokerCache.TryGetValue(method, out ProbeInvoker? cached))
                return cached;
            return _invokerCache.GetOrAdd(method, _builder.BuildInvoker(method));
        }
    }
}
