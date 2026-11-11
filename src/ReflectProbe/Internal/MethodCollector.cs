using System;
using System.Collections.Generic;
using System.Reflection;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// 생성자 · 메서드 검색. 필드 수집(MemberCollector)과 같은 규칙을 쓴다.
    /// - BaseType 체인을 올라가며 타입마다 DeclaredOnly 로 수집 (부모 private 포함)
    /// - override 된 메서드는 가장 파생된 것 1개만, 시그니처가 같은 new 메서드는 자식 우선
    /// - 프로퍼티 접근자 · 연산자 등 특수 이름, 제네릭 메서드 정의, ref · 포인터 · ref struct 시그니처 제외
    /// - 매개변수 타입은 정확히 일치해야 한다. (Type.GetMethod 의 기본 Binder 는 대입 가능한 타입도 골라 버린다)
    /// </summary>
    internal static class MethodCollector
    {
        /// <summary>이름 → 오버로드 목록. 같은 이름 안의 순서는 파생 타입 쪽이 먼저다.</summary>
        public static Dictionary<string, MethodInfo[]> Collect(Type type, ProbeScope scope)
        {
            BindingFlags flags = MemberCollector.ToBindingFlags(scope) | BindingFlags.DeclaredOnly;
            var byName = new Dictionary<string, List<MethodInfo>>(StringComparer.Ordinal);

            // 이미 수집한 가상 메서드의 최초 정의. 부모 쪽에서 같은 정의를 만나면 자식이 override 한 것이라 건너뛴다.
            var collectedDefinitions = new HashSet<MethodInfo>();

            for (Type? current = type; current != null; current = current.BaseType)
            {
                foreach (MethodInfo method in current.GetMethods(flags))
                {
                    if (method.IsSpecialName || method.ContainsGenericParameters || HasUnsupportedSignature(method))
                        continue;

                    if (method.IsVirtual && !collectedDefinitions.Add(method.GetBaseDefinition()))
                        continue;

                    if (!byName.TryGetValue(method.Name, out List<MethodInfo>? overloads))
                    {
                        overloads = new List<MethodInfo>();
                        byName.Add(method.Name, overloads);
                    }
                    else if (ContainsSameParameters(overloads, method))
                    {
                        continue; // 자식의 new 메서드에 가려진 부모 메서드
                    }

                    overloads.Add(method);
                }
            }

            var result = new Dictionary<string, MethodInfo[]>(byName.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, List<MethodInfo>> pair in byName)
                result.Add(pair.Key, pair.Value.ToArray());
            return result;
        }

        /// <summary>매개변수 타입이 정확히 일치하는 인스턴스 생성자. 범위(Public · NonPublic)를 따른다. 없으면 null.</summary>
        public static ConstructorInfo? FindConstructor(Type type, ProbeScope scope, Type[] parameterTypes)
        {
            if (type.IsAbstract)
                return null; // 추상 클래스 · 인터페이스 · static 클래스는 newobj 할 수 없다

            BindingFlags flags = (MemberCollector.ToBindingFlags(scope) & (BindingFlags.Public | BindingFlags.NonPublic)) | BindingFlags.Instance;
            foreach (ConstructorInfo constructor in type.GetConstructors(flags))
            {
                if (ParametersEqual(constructor.GetParameters(), parameterTypes))
                    return constructor;
            }

            return null;
        }

        public static bool ParametersEqual(ParameterInfo[] parameters, Type[] parameterTypes)
        {
            if (parameters.Length != parameterTypes.Length)
                return false;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != parameterTypes[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 강타입 Invoker 시그니처 일치 (결정 27).
        /// static : 델리게이트 매개변수 = 메서드 매개변수. 인스턴스 : 첫 매개변수가 대상, 나머지 = 메서드 매개변수.
        /// 매개변수 · 반환 타입은 정확히 일치해야 하고, 대상만 IsTargetCompatible 규칙으로 변환을 허용한다.
        /// </summary>
        public static bool MatchesInvoker(MethodInfo method, Type[] delegateParameters, Type delegateReturn)
        {
            if (method.ReturnType != delegateReturn)
                return false;

            ParameterInfo[] parameters = method.GetParameters();
            int offset = method.IsStatic ? 0 : 1;
            if (delegateParameters.Length != parameters.Length + offset)
                return false;
            if (!method.IsStatic && !IsTargetCompatible(delegateParameters[0], method.DeclaringType!))
                return false;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != delegateParameters[i + offset])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 대상 매개변수 규칙. struct 는 T(사본) 또는 ref T(원본).
        /// class 는 선언 타입 · 파생 타입(변환 없음) 또는 부모 타입 · object · 인터페이스(castclass).
        /// </summary>
        private static bool IsTargetCompatible(Type target, Type declaringType)
        {
            if (declaringType.IsValueType)
                return target == declaringType || target == declaringType.MakeByRefType();

            return !target.IsValueType && !target.IsByRef
                && (declaringType.IsAssignableFrom(target) || target.IsAssignableFrom(declaringType));
        }

        /// <summary>예외 메시지용 시그니처 문자열. "Attack(Int32, Int32)"</summary>
        public static string FormatSignature(string name, Type[] parameterTypes)
        {
            var names = new string[parameterTypes.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = parameterTypes[i]?.Name ?? "null";
            return name + "(" + string.Join(", ", names) + ")";
        }

        /// <summary>예외 메시지용 강타입 시그니처. "Attack(Enemy, Int32) : Int32"</summary>
        public static string FormatSignature(string name, Type[] parameterTypes, Type returnType)
            => FormatSignature(name, parameterTypes) + " : " + returnType.Name;

        private static bool ContainsSameParameters(List<MethodInfo> overloads, MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var parameterTypes = new Type[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                parameterTypes[i] = parameters[i].ParameterType;

            foreach (MethodInfo overload in overloads)
            {
                if (ParametersEqual(overload.GetParameters(), parameterTypes))
                    return true;
            }

            return false;
        }

        private static bool HasUnsupportedSignature(MethodInfo method)
        {
            if (MemberCollector.IsUnsupportedType(method.ReturnType))
                return true;

            foreach (ParameterInfo parameter in method.GetParameters())
            {
                if (MemberCollector.IsUnsupportedType(parameter.ParameterType))
                    return true;
            }

            return false;
        }
    }
}
