namespace ReflectProbe
{
    /// <summary>
    /// 접근 코드를 만드는 방식. 프로세스당 1회 판단한다.
    /// </summary>
    public enum ProbeMode
    {
        /// <summary>DynamicMethod 로 IL 을 생성한다. (CoreCLR, Mono JIT)</summary>
        Emit,

        /// <summary>동적 코드 생성이 불가능한 환경(IL2CPP, NativeAOT)의 폴백. 캐싱한 MemberInfo 로 접근한다.</summary>
        Reflection
    }
}
