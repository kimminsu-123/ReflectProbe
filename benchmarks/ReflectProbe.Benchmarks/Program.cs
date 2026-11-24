using BenchmarkDotNet.Running;
using ReflectProbe.Benchmarks;

// dotnet run -c Release -- --filter *            모든 벤치마크
// dotnet run -c Release -- --filter * --job dry  동작 확인만 (수치는 쓰지 않는다)
// dotnet run -c Release -- concurrency           동시 최초 생성의 중복 · 예외 확인
if (args is ["concurrency"])
    ConcurrencyCheck.Run();
else if (args is ["concurrency-init", string mode])
    ConcurrencyCheck.RunInit(mode); // ConcurrencyCheck 가 띄우는 자식 프로세스
else
    BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
