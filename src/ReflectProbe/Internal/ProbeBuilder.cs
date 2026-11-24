using System;
using System.Reflection;
using System.Threading;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// 접근 델리게이트를 만드는 전략. TypeProbe 는 결과 델리게이트만 들고 있어서 히트 경로에 가상 호출 단계가 없다.
    /// Emit 과 Reflection(AOT 폴백) 두 구현이 같은 시그니처의 델리게이트를 만든다.
    /// </summary>
    internal abstract class ProbeBuilder
    {
        private static ProbeBuilder? _current;

        /// <summary>DynamicCodeSupport 판단 결과에 따른 프로세스 기본 빌더. 처음 읽을 때 1회 정해진다.</summary>
        public static ProbeBuilder Current
            => Volatile.Read(ref _current) ?? InitializeCurrent(DynamicCodeSupport.IsSupported ? new EmitProbeBuilder() : new ReflectionProbeBuilder());

        /// <summary>
        /// 테스트 전용 : 프로세스 기본 빌더를 고정한다. Current 가 한 번이라도 읽히기 전에 불러야 한다
        /// (Reflection 모드 테스트 프로젝트의 모듈 초기화). 이미 다른 빌더로 정해졌으면 InvalidOperationException.
        /// </summary>
        internal static void ForceCurrent(ProbeBuilder builder)
        {
            if (InitializeCurrent(builder) != builder)
                throw new InvalidOperationException($"ProbeBuilder.Current 가 이미 {_current!.Mode} 로 정해졌습니다.");
        }

        private static ProbeBuilder InitializeCurrent(ProbeBuilder builder)
            => Interlocked.CompareExchange(ref _current, builder, null) ?? builder;

        public abstract ProbeMode Mode { get; }

        /// <summary>(index, target) → value. Emit 구현은 DynamicMethod 1개 안의 IL switch 로 분기한다.</summary>
        public abstract Func<int, object?, object?> BuildGetter(Type type, ProbeMember[] members);

        /// <summary>(index, target, value). 쓸 수 없는 멤버의 case 는 MemberNotWritableException 으로 모인다.</summary>
        public abstract Action<int, object?, object?> BuildSetter(Type type, ProbeMember[] members);

        public abstract Getter<T, TValue> BuildGetter<T, TValue>(ProbeMember member);

        public abstract Setter<T, TValue> BuildSetter<T, TValue>(ProbeMember member);

        public abstract RefSetter<T, TValue> BuildRefSetter<T, TValue>(ProbeMember member);

        /// <summary>
        /// 매개변수 없는 생성자로 T 를 만든다. constructor 가 null 이면 값 타입 기본값(initobj).
        /// 생성 가능 여부(추상 타입, 생성자 없음)는 호출 측(TypeProbe)이 판단한다.
        /// </summary>
        public abstract Func<T> BuildDefaultFactory<T>(ConstructorInfo? constructor);

        /// <summary>args[i] 를 매개변수 타입으로 바꿔 생성자를 호출한다. 값 타입 결과는 박싱한다.</summary>
        public abstract ProbeFactory BuildFactory(ConstructorInfo constructor);

        /// <summary>target · args 를 받아 메서드를 호출한다. void 는 null, 값 타입 반환은 박싱한다.</summary>
        public abstract ProbeInvoker BuildInvoker(MethodInfo method);

        /// <summary>
        /// delegateType 시그니처 그대로 생성자를 호출하는 델리게이트. 인자 배열 · 박싱이 없다.
        /// 시그니처 일치 여부는 호출 측(MethodCollector.MatchesFactory)이 확인한다.
        /// </summary>
        public abstract Delegate BuildTypedFactory(ConstructorInfo constructor, Type delegateType);

        /// <summary>
        /// delegateType 시그니처 그대로 메서드를 호출하는 델리게이트. 인스턴스 메서드는 첫 매개변수가 대상이다.
        /// 시그니처 일치 여부는 호출 측(MethodCollector.MatchesInvoker)이 확인한다.
        /// </summary>
        public abstract Delegate BuildTypedInvoker(MethodInfo method, Type delegateType);

        // ---------- 강타입 접근 검사 (두 구현 공용, 델리게이트를 만들 때 1회) ----------

        /// <summary>읽을 수 있고 멤버 타입 → TValue 변환이 가능해야 한다.</summary>
        private protected static void EnsureGettable<TValue>(ProbeMember member)
        {
            if (!member.CanRead)
                throw new MemberNotReadableException(member.DeclaringType, member.Name);
            EnsureConvertible(member, typeof(TValue));
        }

        /// <summary>쓸 수 있고 TValue → 멤버 타입 변환이 가능해야 한다.</summary>
        private protected static void EnsureSettable<TValue>(ProbeMember member)
        {
            if (!member.CanWrite)
                throw new MemberNotWritableException(member.DeclaringType, member.Name);
            EnsureConvertible(member, typeof(TValue));
        }

        /// <summary>
        /// Setter 는 대상을 값으로 받으므로 struct 인스턴스 멤버는 사본에만 써진다 (결정 4). static 멤버는 대상을 쓰지 않아 허용한다.
        /// </summary>
        private protected static void EnsureSettableByValue<T, TValue>(ProbeMember member)
        {
            EnsureSettable<TValue>(member);
            if (typeof(T).IsValueType && !member.IsStatic)
                throw new NotSupportedException(
                    $"값 타입 '{typeof(T)}' 의 '{member.Name}' 에는 Setter 를 쓸 수 없습니다 (사본에만 써집니다). RefSetter 를 쓰세요.");
        }

        /// <summary>
        /// 결정 35 : 정확 일치 · 참조 변환 · 박싱/언박싱만 허용한다. 서로 다른 값 타입은 변환 IL 이 없어 비트를 그대로 재해석하게 되므로 막는다.
        /// 참조 변환 · 언박싱의 실제 타입 검사는 호출할 때 castclass / unbox.any 가 한다.
        /// </summary>
        private static void EnsureConvertible(ProbeMember member, Type valueType)
        {
            if (member.Type != valueType && member.Type.IsValueType && valueType.IsValueType)
                throw new InvalidCastException(
                    $"'{member.DeclaringType}.{member.Name}' 의 타입은 '{member.Type}' 이라 '{valueType}' 로 읽거나 쓸 수 없습니다.");
        }
    }
}
