using System.Reflection;
using System.Runtime.Versioning;
using ReflectProbe.Internal;

namespace ReflectProbe.Tests;

public class ProbeModeTests
{
    [Fact]
    public void Mode_MatchesTestProject()
    {
#if REFLECTION_MODE
        Assert.Equal(ProbeMode.Reflection, TypeProbe.Mode);
        Assert.IsType<ReflectionProbeBuilder>(ProbeBuilder.Current);
#else
        Assert.Equal(ProbeMode.Emit, TypeProbe.Mode);
        Assert.IsType<EmitProbeBuilder>(ProbeBuilder.Current);
#endif
    }

    [Fact]
    public void DynamicCodeSupport_IsTrueOnCoreClr()
    {
        // 감지 결과는 강제 모드와 무관하다. CoreCLR 은 API · 시험 생성 모두 통과해야 한다.
        Assert.True(DynamicCodeSupport.IsSupported);
    }

    [Fact]
    public void ForceCurrent_AfterInitialization_Throws()
    {
        _ = ProbeBuilder.Current;
        ProbeBuilder other = ProbeBuilder.Current is EmitProbeBuilder ? new ReflectionProbeBuilder() : new EmitProbeBuilder();

        Assert.Throws<InvalidOperationException>(() => ProbeBuilder.ForceCurrent(other));
        Assert.Equal(TypeProbe.Mode, ProbeBuilder.Current.Mode);   // 바뀌지 않음
    }

    [Fact]
    public void LibraryAssembly_MatchesRequestedTargetFramework()
    {
        string? framework = typeof(TypeProbe).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

#if PROBE_NETSTANDARD
        Assert.Equal(".NETStandard,Version=v2.1", framework);
#else
        Assert.Equal(".NETCoreApp,Version=v8.0", framework);
#endif
    }

    [Fact]
    public void ProbeInstance_UsesCurrentBuilder()
    {
        // 캐시(Of) 경로가 Current 빌더로 만든다 : 모드별로 같은 테스트 세트가 실제로 다른 경로를 타는지 확인.
        Getter<Player, int> getter = TypeProbe.Of<Player>().Getter<int>("_hp");
        bool isDynamicMethod = getter.Method.DeclaringType == null;   // DynamicMethod 는 선언 타입이 없다. 폴백은 C# 람다(클로저 클래스).

#if REFLECTION_MODE
        Assert.False(isDynamicMethod);
#else
        Assert.True(isDynamicMethod);
#endif
    }
}
