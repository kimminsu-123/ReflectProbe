# 같은 테스트 세트를 라이브러리 결과물(net8.0 / netstandard2.1) × 접근 코드 생성 방식(Emit / Reflection) 4가지로 실행한다.
# 실행마다 --artifacts-path 를 나눠 결과물이 섞이지 않게 한다.
#   pwsh tests/run-matrix.ps1 [-Configuration Release]
param(
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$runs = @(
    @{ Name = 'emit-net8.0';          Project = 'tests/ReflectProbe.Tests';            Tfm = '' },
    @{ Name = 'reflection-net8.0';    Project = 'tests/ReflectProbe.Tests.Reflection'; Tfm = '' },
    @{ Name = 'emit-netstandard2.1';  Project = 'tests/ReflectProbe.Tests';            Tfm = 'netstandard2.1' },
    @{ Name = 'reflection-netstandard2.1'; Project = 'tests/ReflectProbe.Tests.Reflection'; Tfm = 'netstandard2.1' }
)

$failed = @()
foreach ($run in $runs) {
    Write-Host "`n=== $($run.Name) ===" -ForegroundColor Cyan
    $arguments = @('test', (Join-Path $root $run.Project), '-c', $Configuration,
        '--artifacts-path', (Join-Path $root "artifacts/test-matrix/$($run.Name)"))
    if ($run.Tfm) {
        $arguments += "-p:ProbeLibraryTfm=$($run.Tfm)"
    }

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        $failed += $run.Name
    }
}

if ($failed.Count -gt 0) {
    Write-Host "`n실패 : $($failed -join ', ')" -ForegroundColor Red
    exit 1
}
Write-Host "`n4가지 실행 모두 통과" -ForegroundColor Green
