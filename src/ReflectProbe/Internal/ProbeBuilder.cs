using System;
using System.Reflection;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// 접근 델리게이트를 만드는 전략. TypeProbe 는 결과 델리게이트만 들고 있어서 히트 경로에 가상 호출 단계가 없다.
    /// Emit 과 Reflection(AOT 폴백) 두 구현이 같은 시그니처의 델리게이트를 만든다.
    /// </summary>
    internal abstract class ProbeBuilder
    {
        /// <summary>DynamicCodeSupport 판단 결과에 따른 프로세스 기본 빌더.</summary>
        /// <remarks>Reflection 폴백 연동(13주) 전까지는 Emit 고정.</remarks>
        public static ProbeBuilder Current { get; } = new EmitProbeBuilder();

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
    }
}
