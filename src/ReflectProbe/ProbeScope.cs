using System;

namespace ReflectProbe
{
    /// <summary>
    /// 수집할 멤버의 범위. FastMember 의 allowNonPublicAccessors(bool) 대신 BindingFlags 처럼 명시한다.
    /// 캐시 키는 (Type, ProbeScope) 이다.
    /// </summary>
    [Flags]
    public enum ProbeScope
    {
        /// <summary>public 멤버.</summary>
        Public = 1,

        /// <summary>private · protected · internal 멤버. 부모의 private 멤버도 포함한다.</summary>
        NonPublic = 2,

        /// <summary>인스턴스 멤버.</summary>
        Instance = 4,

        /// <summary>static 멤버.</summary>
        Static = 8,

        /// <summary>TypeProbe.Of 의 기본값.</summary>
        All = Public | NonPublic | Instance | Static
    }
}
