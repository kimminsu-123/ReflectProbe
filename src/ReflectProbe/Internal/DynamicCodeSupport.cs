using System;

namespace ReflectProbe.Internal
{
    /// <summary>
    /// 동적 코드 생성 가능 여부를 프로세스당 1회 판단한다.
    /// - net8.0 : RuntimeFeature.IsDynamicCodeSupported
    /// - netstandard2.1 : 작은 DynamicMethod 를 1회 생성·호출해 PlatformNotSupportedException / NotSupportedException 이면 불가
    ///   (netstandard2.1 표면에 IsDynamicCodeSupported 가 있는지는 구현 시 공식 문서로 확인)
    /// </summary>
    internal static class DynamicCodeSupport
    {
        public static bool IsSupported => throw new NotImplementedException();
    }
}
