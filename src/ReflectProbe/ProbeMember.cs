using System;
using System.Reflection;

namespace ReflectProbe
{
    public enum ProbeMemberKind
    {
        Field,
        Property
    }

    /// <summary>
    /// 수집된 필드 또는 프로퍼티 1개의 메타데이터. 생성 시점에 모든 값을 계산해 두고 이후 변하지 않는다.
    /// </summary>
    public sealed class ProbeMember
    {
        // scope 는 프로퍼티 접근자의 공개 여부를 CanRead / CanWrite 에 반영하기 위해 받는다.
        internal ProbeMember(MemberInfo info, int index, bool isHidden, ProbeScope scope)
        {
            Info = info;
            Index = index;
            IsHidden = isHidden;
            Name = info.Name;
            DeclaringType = info.DeclaringType!;

            switch (info)
            {
                case FieldInfo field:
                    Kind = ProbeMemberKind.Field;
                    Type = field.FieldType;
                    IsStatic = field.IsStatic;
                    IsPublic = field.IsPublic;
                    CanRead = true;
                    CanWrite = !(field.IsStatic && field.IsInitOnly);
                    break;

                case PropertyInfo property:
                    bool allowNonPublic = (scope & ProbeScope.NonPublic) != 0;
                    GetMethod = FindAccessor(property, getter: true);
                    SetMethod = FindAccessor(property, getter: false);
                    Kind = ProbeMemberKind.Property;
                    Type = property.PropertyType;
                    IsStatic = (GetMethod ?? SetMethod)!.IsStatic;
                    IsPublic = GetMethod?.IsPublic == true || SetMethod?.IsPublic == true;
                    CanRead = GetMethod != null && (allowNonPublic || GetMethod.IsPublic);
                    CanWrite = SetMethod != null && (allowNonPublic || SetMethod.IsPublic);
                    break;

                default:
                    throw new ArgumentException($"필드나 프로퍼티가 아닙니다: {info}", nameof(info));
            }
        }

        public string Name { get; }

        /// <summary>필드 타입 또는 프로퍼티 타입.</summary>
        public Type Type { get; }

        public ProbeMemberKind Kind { get; }

        public Type DeclaringType { get; }

        public bool IsStatic { get; }

        public bool IsPublic { get; }

        /// <summary>FastMember 와 달리 필드에서도 예외 없이 동작한다.</summary>
        public bool CanRead { get; }

        /// <summary>static readonly 필드, setter 없는 프로퍼티는 false. 인스턴스 readonly 필드는 true.</summary>
        public bool CanWrite { get; }

        /// <summary>TypeProbe.GetValue / SetValue 의 인덱스.</summary>
        public int Index { get; }

        /// <summary>파생 타입의 같은 이름 멤버에 가려져 이름으로는 접근할 수 없는 멤버. 인덱스로는 접근 가능하다.</summary>
        public bool IsHidden { get; }

        /// <summary>Attribute 조회 등 추가 정보가 필요할 때 쓰는 원본 MemberInfo.</summary>
        public MemberInfo Info { get; }

        /// <summary>프로퍼티 getter. 상속받은 접근자 포함. 필드는 null.</summary>
        internal MethodInfo? GetMethod { get; }

        /// <summary>프로퍼티 setter. 상속받은 접근자 포함. 필드는 null.</summary>
        internal MethodInfo? SetMethod { get; }

        public override string ToString() => $"{Kind} {Type.Name} {Name}";

        /// <summary>
        /// getter 만 override 하면 파생 타입의 PropertyInfo.SetMethod 는 null 이다. (반대도 같음)
        /// 이때 같은 가상 프로퍼티를 선언한 부모에서 빠진 접근자를 찾는다. 가상 호출이라 실제 동작은 override 규칙을 따른다.
        /// </summary>
        private static MethodInfo? FindAccessor(PropertyInfo property, bool getter)
        {
            MethodInfo? own = getter ? property.GetMethod : property.SetMethod;
            if (own != null)
                return own;

            MethodInfo? other = getter ? property.SetMethod : property.GetMethod;
            if (other == null || !other.IsVirtual)
                return null;

            MethodInfo otherDefinition = other.GetBaseDefinition();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

            for (Type? current = property.DeclaringType!.BaseType; current != null; current = current.BaseType)
            {
                foreach (PropertyInfo candidate in current.GetProperties(flags))
                {
                    MethodInfo? candidateOther = getter ? candidate.SetMethod : candidate.GetMethod;
                    if (candidate.Name != property.Name || candidateOther?.GetBaseDefinition() != otherDefinition)
                        continue;

                    MethodInfo? accessor = getter ? candidate.GetMethod : candidate.SetMethod;
                    if (accessor != null)
                        return accessor;
                }
            }

            return null;
        }
    }
}
