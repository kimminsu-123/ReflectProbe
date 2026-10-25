using System;
using System.Reflection.Emit;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// ReflectionBenchmark/EmitHelpers.cs 의 변환 규칙을 옮겨 온다.
    /// object → T : 참조 타입 castclass, 값 타입 unbox.any (대상 객체 자리는 unbox 로 박스 안 주소)
    /// T → object : 값 타입만 box
    /// </summary>
    internal static class ILEmitHelpers
    {
        public static void EmitConvertFromObject(ILGenerator il, Type target) => throw new NotImplementedException();

        public static void EmitConvertToObject(ILGenerator il, Type source) => throw new NotImplementedException();

        /// <summary>object 로 받은 대상 객체를 멤버 접근이 가능한 형태(참조 또는 박스 안 주소)로 바꾼다.</summary>
        public static void EmitLoadTarget(ILGenerator il, Type declaringType) => throw new NotImplementedException();
    }
}
