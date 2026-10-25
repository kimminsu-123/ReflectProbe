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
        internal ProbeMember(MemberInfo info, int index, bool isHidden)
        {
            throw new NotImplementedException();
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

        public override string ToString() => $"{Kind} {Type.Name} {Name}";
    }
}
