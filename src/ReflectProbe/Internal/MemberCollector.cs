using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// 범위에 맞는 필드·프로퍼티를 수집해 Index 순서의 멤버 목록을 만든다. 이름 맵은 이 목록으로 ProbeMemberSet 이 만든다.
    /// - 인덱서, [CompilerGenerated] 백킹 필드, const 필드, 박싱할 수 없는 타입(ref · 포인터 · ref struct) 제외
    /// - BaseType 체인을 올라가며 타입마다 DeclaredOnly 로 수집 (GetFields(NonPublic) 는 부모 private 을 돌려주지 않음)
    /// - override 된 프로퍼티는 가장 파생된 것 1개만
    /// - 같은 이름은 가장 파생된 타입의 멤버만 이름으로 접근, 나머지는 IsHidden = true (이름 + DepthOffset 으로는 접근 가능)
    /// </summary>
    internal static class MemberCollector
    {
        public static ProbeMember[] Collect(Type type, ProbeScope scope)
        {
            BindingFlags flags = ToBindingFlags(scope) | BindingFlags.DeclaredOnly;
            var infos = new List<MemberInfo>();
            var depths = new List<int>();  // infos 와 같은 순서. BaseType 체인에서 몇 단계 위에서 선언됐는지

            // 이미 수집한 프로퍼티 접근자의 최초 정의. 부모 쪽에서 같은 정의를 만나면 override 된 것이라 건너뛴다.
            var collectedAccessors = new HashSet<MethodInfo>();

            int depth = 0;
            for (Type? current = type; current != null; current = current.BaseType, depth++)
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
                    if (property.GetIndexParameters().Length > 0 || IsUnsupportedType(property.PropertyType))
                        continue;

                    if (!AddAccessors(collectedAccessors, property))
                        continue;

                    infos.Add(property);
                    depths.Add(depth);
                }
            }

            var members = new ProbeMember[infos.Count];
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < members.Length; i++)
            {
                bool isHidden = !names.Add(infos[i].Name);
                members[i] = new ProbeMember(infos[i], i, isHidden, depths[i], scope);
            }

            return members;
        }

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

        internal static bool IsUnsupportedType(Type type) => type.IsByRef || type.IsPointer || type.IsByRefLike;
    }
}
