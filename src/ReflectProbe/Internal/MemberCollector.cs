using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// 범위에 맞는 필드·프로퍼티를 수집해 Index 순서의 멤버 목록을 만든다. 이름 맵은 이 목록으로 ProbeMemberSet 이 만든다.
    /// - 인덱서, [CompilerGenerated] 백킹 필드, const 필드, 박싱할 수 없는 타입(ref · 포인터 · 함수 포인터 · ref struct) 제외
    /// - BaseType 체인을 올라가며 타입마다 DeclaredOnly 로 수집 (GetFields(NonPublic) 는 부모 private 을 돌려주지 않음)
    /// - override 된 프로퍼티는 가장 파생된 것 1개만
    /// - 같은 이름은 가장 파생된 타입의 멤버만 이름으로 접근, 나머지는 IsHidden = true (이름 + DepthOffset 으로는 접근 가능)
    /// - 인터페이스 대상은 상속한 인터페이스도 수집한다. 상속 관계가 없는 부모끼리 같은 이름이면 어느 쪽도 가려지지 않는다 (이름 접근은 모호, 결정 38)
    /// - static abstract / static virtual 프로퍼티 제외 (타입 인수 없이는 호출할 수 없다)
    /// </summary>
    internal static class MemberCollector
    {
        public static ProbeMember[] Collect(Type type, ProbeScope scope)
        {
            BindingFlags flags = ToBindingFlags(scope) | BindingFlags.DeclaredOnly;
            var infos = new List<MemberInfo>();
            var depths = new List<int>();  // infos 와 같은 순서. 대상 타입에서 몇 단계 위에서 선언됐는지

            // 이미 수집한 프로퍼티 접근자의 최초 정의. 부모 쪽에서 같은 정의를 만나면 override 된 것이라 건너뛴다.
            var collectedAccessors = new HashSet<MethodInfo>();

            foreach ((Type current, int depth) in DeclaringTypes(type))
            {
                foreach (FieldInfo field in current.GetFields(flags))
                {
                    if (field.IsLiteral
                        || IsUnsupportedType(field.FieldType)
                        || field.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
                        continue;

                    infos.Add(field);
                    depths.Add(depth);
                }

                foreach (PropertyInfo property in current.GetProperties(flags))
                {
                    if (property.GetIndexParameters().Length > 0
                        || IsUnsupportedType(property.PropertyType)
                        || IsStaticVirtual(property.GetMethod ?? property.SetMethod!))
                        continue;

                    if (!AddAccessors(collectedAccessors, property))
                        continue;

                    infos.Add(property);
                    depths.Add(depth);
                }
            }

            var members = new ProbeMember[infos.Count];
            var ownersByName = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
            for (int i = 0; i < members.Length; i++)
            {
                Type owner = infos[i].DeclaringType!;
                if (!ownersByName.TryGetValue(infos[i].Name, out List<Type>? owners))
                {
                    owners = new List<Type>();
                    ownersByName.Add(infos[i].Name, owners);
                }

                bool isHidden = owners.Exists(derived => Inherits(derived, owner));
                owners.Add(owner);
                members[i] = new ProbeMember(infos[i], i, isHidden, depths[i], scope);
            }

            return members;
        }

        /// <summary>
        /// 멤버를 수집할 타입과 깊이. class 는 BaseType 체인, 인터페이스는 자신과 상속한 인터페이스 전부.
        /// 인터페이스의 깊이는 가장 긴 상속 경로의 길이라, 가리는 쪽이 항상 가려지는 쪽보다 먼저 나온다.
        /// </summary>
        internal static List<(Type Type, int Depth)> DeclaringTypes(Type type)
        {
            var result = new List<(Type Type, int Depth)>();
            if (!type.IsInterface)
            {
                for (Type? current = type; current != null; current = current.BaseType)
                    result.Add((current, result.Count));
                return result;
            }

            Type[] inherited = type.GetInterfaces();
            result.Add((type, 0));
            foreach (Type parent in inherited)
                result.Add((parent, InterfaceDepth(parent, inherited)));

            result.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            return result;
        }

        private static int InterfaceDepth(Type target, Type[] inherited)
        {
            int depth = 1;
            foreach (Type child in inherited)
            {
                if (child != target && Inherits(child, target))
                    depth = Math.Max(depth, InterfaceDepth(child, inherited) + 1);
            }

            return depth;
        }

        /// <summary>derived 가 ancestor 와 같거나 상속한다. IsAssignableFrom 은 제네릭 변성까지 참이라 쓰지 않는다.</summary>
        internal static bool Inherits(Type derived, Type ancestor)
            => derived == ancestor
            || (ancestor.IsInterface ? Array.IndexOf(derived.GetInterfaces(), ancestor) >= 0 : derived.IsSubclassOf(ancestor));

        /// <summary>인터페이스의 static abstract / static virtual. 구현 타입을 정하는 타입 인수(constrained 호출)가 있어야 부를 수 있다.</summary>
        internal static bool IsStaticVirtual(MethodInfo method) => method.IsStatic && method.IsVirtual;

        /// <summary>접근자의 최초 정의를 등록한다. 이미 등록된 정의가 있으면(= 자식이 override) false.</summary>
        private static bool AddAccessors(HashSet<MethodInfo> collected, PropertyInfo property)
        {
            MethodInfo? getter = property.GetMethod?.GetBaseDefinition();
            MethodInfo? setter = property.SetMethod?.GetBaseDefinition();

            if ((getter != null && collected.Contains(getter)) || (setter != null && collected.Contains(setter)))
                return false;

            if (getter != null)
                collected.Add(getter);
            if (setter != null)
                collected.Add(setter);
            return true;
        }

        internal static BindingFlags ToBindingFlags(ProbeScope scope)
        {
            BindingFlags flags = 0;
            if ((scope & ProbeScope.Public) != 0)
                flags |= BindingFlags.Public;
            if ((scope & ProbeScope.NonPublic) != 0)
                flags |= BindingFlags.NonPublic;
            if ((scope & ProbeScope.Instance) != 0)
                flags |= BindingFlags.Instance;
            if ((scope & ProbeScope.Static) != 0)
                flags |= BindingFlags.Static;
            return flags;
        }

        /// <summary>
        /// 박싱할 수 없는 타입 (ref struct · ref · 포인터 · 함수 포인터).
        /// Type.IsFunctionPointer 는 .NET 8 부터라 "BaseType 이 없는데 object · 인터페이스가 아닌 타입" 으로 판단한다.
        /// ref · 포인터는 따로 검사한다. Unity IL2CPP 는 ref 타입의 BaseType 이 null 이 아니다 (14주 WebGL 실측).
        /// </summary>
        internal static bool IsUnsupportedType(Type type)
            => type.IsByRefLike || type.IsByRef || type.IsPointer
            || (type.BaseType == null && !type.IsInterface && type != typeof(object));
    }
}
