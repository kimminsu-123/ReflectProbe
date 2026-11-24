using System.Runtime.CompilerServices;
using ReflectProbe.Internal;

namespace ReflectProbe.Tests;

internal static class ForceReflectionMode
{
    /// <summary>
    /// 이 어셈블리의 어떤 코드보다 먼저 실행된다 → 테스트가 TypeProbe 를 처음 만들기 전에 폴백 빌더로 고정.
    /// CoreCLR 에서는 감지 결과가 Emit 이므로, 공개 스위치 없이 폴백 경로를 검증하는 유일한 방법이다.
    /// </summary>
#pragma warning disable CA2255 // 테스트 어셈블리 전용 모듈 초기화
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize() => ProbeBuilder.ForceCurrent(new ReflectionProbeBuilder());
}
