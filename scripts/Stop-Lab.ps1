[CmdletBinding()]
param([switch]$RemoveDatabase)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
Push-Location $repository
try {
    if ($RemoveDatabase) {
        Write-Warning 'Se eliminará el volumen de MySQL y todos los datos de NetWatch.'
        & docker compose down --volumes
    }
    else {
        & docker compose down
    }
    if ($LASTEXITCODE -ne 0) { throw 'Docker Compose no pudo detener NetWatch.' }
}
finally {
    Pop-Location
}
