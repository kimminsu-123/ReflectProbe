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
        public static ProbeBuilder Current => throw new NotImplementedException();

        public abstract ProbeMode Mode { get; }

        /// <summary>(index, target) → value. Emit 구현은 DynamicMethod 1개 안의 IL switch 로 분기한다.</summary>
        public abstract Func<int, object?, object?> BuildGetter(Type type, ProbeMember[] members);

        /// <summary>(index, target, value). 쓸 수 없는 멤버의 case 는 MemberNotWritableException 으로 모인다.</summary>
        public abstract Action<int, object?, object?> BuildSetter(Type type, ProbeMember[] members);

        public abstract Getter<T, TValue> BuildGetter<T, TValue>(ProbeMember member);

        public abstract Setter<T, TValue> BuildSetter<T, TValue>(ProbeMember member);

        public abstract RefSetter<T, TValue> BuildRefSetter<T, TValue>(ProbeMember member);

        /// <summary>매개변수 없는 생성자 또는 값 타입 기본값. 둘 다 불가능하면 null.</summary>
        public abstract Func<object>? BuildDefaultFactory(Type type);

        public abstract ProbeFactory BuildFactory(ConstructorInfo constructor);

        public abstract ProbeInvoker BuildInvoker(MethodInfo method);
    }
}
