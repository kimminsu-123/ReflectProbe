using System.Reflection;
using System.Reflection.Emit;

namespace ReflectProbe.Benchmarks;

/// <summary>
/// 같은 모양의 public 타입을 런타임에 새로 만든다. 두 라이브러리 모두 타입별 영구 캐시를 비울 수 없어서,
/// "아직 한 번도 쓰지 않은 타입" 을 매번 공급해야 최초 생성을 반복해서 잴 수 있다.
/// </summary>
internal static class DynamicTypes
{
    private static readonly ModuleBuilder _module = AssemblyBuilder
        .DefineDynamicAssembly(new AssemblyName("ReflectProbe.Benchmarks.Dynamic"), AssemblyBuilderAccess.Run)
        .DefineDynamicModule("main");

    private static int _counter;

    /// <summary>public int 필드 F0 … F(fieldCount-1) 를 가진 class. 기본 생성자 포함.</summary>
    public static Type Create(int fieldCount)
    {
        TypeBuilder builder = _module.DefineType("Dynamic" + Interlocked.Increment(ref _counter), TypeAttributes.Public | TypeAttributes.Class);
        builder.DefineDefaultConstructor(MethodAttributes.Public);
        for (int i = 0; i < fieldCount; i++)
            builder.DefineField("F" + i, typeof(int), FieldAttributes.Public);
        return builder.CreateType();
    }
}
