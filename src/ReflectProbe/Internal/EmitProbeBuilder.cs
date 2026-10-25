using System;
using System.Reflection;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// DynamicMethod(owner: 대상 타입, skipVisibility: true) 로 IL 을 생성한다.
    /// TypeBuilder 경로는 쓰지 않는다. (언로드 불가 + FastMember 동적 어셈블리 초기화 경쟁의 원인)
    /// </summary>
    internal sealed class EmitProbeBuilder : ProbeBuilder
    {
        public override ProbeMode Mode => ProbeMode.Emit;

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
