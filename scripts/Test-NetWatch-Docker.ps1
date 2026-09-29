[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$docker = (Get-Command docker -ErrorAction Stop).Source
$resultsDirectory = Join-Path $repository 'TestResults'
New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
$resultName = 'netwatch-docker-{0}.trx' -f (Get-Date -Format 'yyyyMMdd-HHmmss')
$containerCommand = "dotnet restore NetWatch.sln && dotnet build NetWatch.sln -c Release --no-restore && dotnet test tests/NetWatch.Tests/NetWatch.Tests.csproj -c Release --no-build --logger 'trx;LogFileName=$resultName' --results-directory /src/TestResults"

Push-Location $repository
try {
    & $docker run --rm `
        --volume "${repository}:/src" `
        --workdir /src `
        mcr.microsoft.com/dotnet/sdk:8.0.425 `
        bash -lc $containerCommand
    if ($LASTEXITCODE -ne 0) {
        throw 'Falló la verificación de NetWatch dentro del contenedor .NET SDK.'
    }
    $resultPath = Join-Path $resultsDirectory $resultName
    if (-not (Test-Path -LiteralPath $resultPath)) {
        throw 'El contenedor no generó un resultado TRX.'
    }
    [xml]$result = Get-Content -LiteralPath $resultPath -Raw
    $counters = $result.TestRun.ResultSummary.Counters
    if ([int]$counters.total -ne 14 -or [int]$counters.executed -ne 14 -or [int]$counters.passed -ne 14) {
        throw "Resultado inválido en Docker: total=$($counters.total), ejecutadas=$($counters.executed), aprobadas=$($counters.passed). Se esperaban 14/14."
    }
    Write-Host 'Verificación Docker completa: 14 de 14 pruebas aprobadas.' -ForegroundColor Green
}
finally {
    Pop-Location
}
