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
        Public = 1,
        NonPublic = 2,
        Instance = 4,
        Static = 8,

        /// <summary>TypeProbe.Of 의 기본값.</summary>
        All = Public | NonPublic | Instance | Static
    }
}
