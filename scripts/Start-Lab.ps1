[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$environmentPath = Join-Path $repository '.env'

if (-not (Test-Path -LiteralPath $environmentPath)) {
    throw 'Falta .env. Ejecute primero scripts\Initialize-Environment.ps1 como se indica en la guía.'
}

$content = Get-Content -LiteralPath $environmentPath -Raw
if ($content -match 'CAMBIAR_') {
    throw 'El archivo .env todavía contiene valores CAMBIAR_. Corríjalos antes de iniciar.'
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker no está disponible. Instale/inicie Docker Desktop y vuelva a ejecutar.'
}

Push-Location $repository
try {
    & docker compose config --quiet
    if ($LASTEXITCODE -ne 0) { throw 'La configuración de Docker Compose no es válida.' }
    & docker compose up --build -d
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose no pudo iniciar NetWatch.' }
    & docker compose ps
    Write-Host 'Panel: http://localhost:8080'
    Write-Host 'API:   http://localhost:8081/health'
    Write-Host 'MySQL visual (solo host): http://localhost:8082'
}
finally {
    Pop-Location
}
