using BenchmarkDotNet.Running;

// 시나리오는 14주에 추가한다. 실행: dotnet run -c Release -- --filter *
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
