[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$localDotnet = Join-Path $repository '.tools\dotnet\dotnet.exe'
$dotnet = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { (Get-Command dotnet -ErrorAction Stop).Source }

Push-Location $repository
try {
    & $dotnet restore NetWatch.sln
    if ($LASTEXITCODE -ne 0) { throw 'Falló la restauración de paquetes.' }
    & $dotnet build NetWatch.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }
    $resultsDirectory = Join-Path $repository 'TestResults'
    New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
    $resultName = 'netwatch-{0}.trx' -f (Get-Date -Format 'yyyyMMdd-HHmmss')
    & $dotnet test tests\NetWatch.Tests\NetWatch.Tests.csproj -c Release --no-build `
        --logger "trx;LogFileName=$resultName" --results-directory $resultsDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las pruebas automatizadas.' }
    $resultPath = Join-Path $resultsDirectory $resultName
    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw 'El ejecutor no generó un resultado TRX; no se puede afirmar que las pruebas se ejecutaron.'
    }
    [xml]$result = Get-Content -LiteralPath $resultPath -Raw
    $counters = $result.TestRun.ResultSummary.Counters
    if ([int]$counters.total -lt 29 -or [int]$counters.executed -ne [int]$counters.total -or [int]$counters.passed -ne [int]$counters.total) {
        throw "Resultado inválido: total=$($counters.total), ejecutadas=$($counters.executed), aprobadas=$($counters.passed). Deben ejecutarse y aprobarse todas las pruebas, con un mínimo de 29."
    }
    Write-Host "Verificación completa: $($counters.passed) de $($counters.total) pruebas aprobadas." -ForegroundColor Green
}
finally {
    Pop-Location
}
