using System;
using System.Reflection;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// 동적 코드 생성이 불가능한 환경(IL2CPP, NativeAOT)용 폴백.
    /// FieldInfo / PropertyInfo / ConstructorInfo / MethodInfo 를 인덱스 배열로 캐싱해 GetValue / SetValue / Invoke 로 접근한다.
    /// 강타입 델리게이트도 내부적으로 박싱이 남는다.
    /// </summary>
    internal sealed class ReflectionProbeBuilder : ProbeBuilder
    {
        public override ProbeMode Mode => ProbeMode.Reflection;

        public override Func<int, object?, object?> BuildGetter(Type type, ProbeMember[] members) => throw new NotImplementedException();

        public override Action<int, object?, object?> BuildSetter(Type type, ProbeMember[] members) => throw new NotImplementedException();

        public override Getter<T, TValue> BuildGetter<T, TValue>(ProbeMember member) => throw new NotImplementedException();

        public override Setter<T, TValue> BuildSetter<T, TValue>(ProbeMember member) => throw new NotImplementedException();

        public override RefSetter<T, TValue> BuildRefSetter<T, TValue>(ProbeMember member) => throw new NotImplementedException();

        public override Func<object>? BuildDefaultFactory(Type type) => throw new NotImplementedException();

        public override ProbeFactory BuildFactory(ConstructorInfo constructor) => throw new NotImplementedException();

        public override ProbeInvoker BuildInvoker(MethodInfo method) => throw new NotImplementedException();
    }
}
