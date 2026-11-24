namespace ReflectProbe.Tests;

// 이 폴더의 테스트는 두 프로젝트가 같이 컴파일한다.
// - ReflectProbe.Tests            : 기본 경로 (CoreCLR → Emit)
// - ReflectProbe.Tests.Reflection : REFLECTION_MODE 정의 + 모듈 초기화에서 ReflectionProbeBuilder 강제
// 두 프로젝트 모두 -p:ProbeLibraryTfm=netstandard2.1 이면 라이브러리의 netstandard2.1 결과물을 참조한다 (PROBE_NETSTANDARD).

/// <summary>
/// Emit 경로에서만 성립하는 테스트 (박싱 · 인자 배열 없는 할당 0 B 등).
/// Reflection 폴백은 강타입 API 도 내부에서 박싱하므로 건너뛴다 (design.md 4장).
/// </summary>
public sealed class EmitOnlyFactAttribute : FactAttribute
{
    public EmitOnlyFactAttribute()
    {
#if REFLECTION_MODE
        Skip = "Emit 전용 : Reflection 폴백은 박싱 · 인자 배열 할당이 남는다";
#endif
    }
}
