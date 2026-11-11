using System;
using System.Reflection;

namespace ReflectProbe.Internal
{
    /// <summary>강타입 Factory · Invoker 의 시그니처를 델리게이트 타입의 Invoke 메서드에서 읽는다.</summary>
    internal static class DelegateSignature
    {
        public static MethodInfo InvokeMethod(Type delegateType)
        {
            if (!typeof(Delegate).IsAssignableFrom(delegateType) || delegateType == typeof(Delegate) || delegateType == typeof(MulticastDelegate))
                throw new ArgumentException($"'{delegateType}' 은(는) 구체적인 델리게이트 타입이 아닙니다. Func<…> · Action<…> 등을 쓰세요.", nameof(delegateType));

            return delegateType.GetMethod("Invoke")!;
        }

        public static Type[] ParameterTypes(Type delegateType)
        {
            ParameterInfo[] parameters = InvokeMethod(delegateType).GetParameters();
            var types = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                types[i] = parameters[i].ParameterType;
            return types;
        }
    }
}
