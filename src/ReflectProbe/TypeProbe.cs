using System;
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

        private protected TypeProbe(Type type, ProbeScope scope, ProbeBuilder builder)
        {
            Type = type;
            Scope = scope;
            _builder = builder;

            ProbeMember[] members = MemberCollector.Collect(type, scope);
            Members = new ProbeMemberSet(type, members);
            _getter = builder.BuildGetter(type, members);
            _setter = builder.BuildSetter(type, members);
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
