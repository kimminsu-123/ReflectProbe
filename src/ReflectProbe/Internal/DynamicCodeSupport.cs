using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// 동적 코드 생성 가능 여부를 프로세스당 1회 판단한다. 두 TFM 이 같은 코드를 쓴다 (13주 결정 28).
    /// 1. RuntimeFeature.IsDynamicCodeSupported 가 false 면 불가 (NativeAOT 등). netstandard2.1 표면에도 있다.
    /// 2. true 여도 작은 DynamicMethod 를 실제로 만들어 호출해 본다.
    ///    런타임이 값을 정확히 돌려주는지 확실하지 않은 환경(Unity IL2CPP 등)에서 첫 접근 때 예외가 나는 대신 폴백한다.
    /// </summary>
    internal static class DynamicCodeSupport
    {
        public static bool IsSupported { get; } = Detect();

        private static bool Detect()
        {
            if (!RuntimeFeature.IsDynamicCodeSupported)
                return false;

            // 계획서는 PlatformNotSupportedException / NotSupportedException 만 잡는 안이었지만,
            // AOT 런타임마다 던지는 예외 타입이 확실하지 않다 (IL2CPP 는 ExecutionEngineException 일 수도 있음).
            // 여기서 예외가 새면 정적 초기화 실패(TypeInitializationException)로 라이브러리 전체가 막히므로 모두 폴백으로 본다.
            try
            {
                return TryEmit();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>int → int + 1. 생성 · 델리게이트 변환 · 호출 세 단계가 모두 되는지 본다.</summary>
        private static bool TryEmit()
        {
            var method = new DynamicMethod("ReflectProbe_DynamicCodeCheck", typeof(int), new[] { typeof(int) },
                typeof(DynamicCodeSupport).Module, skipVisibility: true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Add);
            il.Emit(OpCodes.Ret);

            var check = (Func<int, int>)method.CreateDelegate(typeof(Func<int, int>));
            return check(41) == 42;
        }
    }
}
